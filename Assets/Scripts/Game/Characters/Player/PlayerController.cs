using UnityEngine;

using System;
using System.Collections;

using LunarCore;

[Serializable]
public class PlayerShotInfo
{
    [SerializeField]
    FireBallController m_prefab;

    [SerializeField]
    Transform m_origin;

    public FireBallController prefab
    {
        get { return m_prefab; }
    }

    public Transform origin
    {
        get { return m_origin; }
    }
}

public class PlayerController : LevelObjectСontroller
{
    enum State
    {
        Small = 0,
        Big,
        Super
    }

    [SerializeField]
    Rect m_BigColliderRect;

    [SerializeField]
    private float m_JumpHighSpeed = 120.0f;

    [SerializeField]
    private float m_JumpSquashSpeed = 70.0f;

    [SerializeField]
    private float m_WalkAcc = 112.0f;

    [SerializeField]
    private float m_WalkSlowAcc = 57.0f;

    [SerializeField]
    private RuntimeAnimatorController m_BigAnimatorController;

    [SerializeField]
    private PlayerShotInfo m_shot;

    State m_State;

    /* User move input */
    Vector2 m_MoveInput;

    bool m_Jumping;
    bool m_Invincible;

    RuntimeAnimatorController m_InitialAnimatorController;
    Rect m_InitialColliderRect;

    #region Lifecycle

    protected override void OnAwake()
    {
        base.OnAwake();

        assert.IsNotNull(m_BigAnimatorController);

        m_InitialAnimatorController = animator.runtimeAnimatorController;
        m_InitialColliderRect = colliderRect;
        m_State = State.Small;
    }

    protected override void OnEnabled()
    {
        base.OnEnabled();

        flipX = false;
        m_MoveInput = Vector2.zero;
    }

    protected override void OnUpdate(float deltaTime)
    {
        if (dead) return;

        m_MoveInput.x = Input.GetAxisRaw("Horizontal");
        m_MoveInput.y = Input.GetAxisRaw("Vertical");
        
        if (Input.GetButtonDown("Jump") && grounded && !m_Jumping)
        {
            StartJump(m_JumpHighSpeed);
        }

        if (Input.GetButtonDown("Shoot"))
        {
            Shot();
        }
    }

    #endregion

    #region Inheritance

    protected override void UpdateVelocity(float deltaTime)
    {
        base.UpdateVelocity(deltaTime);

        float vx = m_Velocity.x;
        float moveX = m_MoveInput.x;
        
        if (Mathf.Approximately(moveX, 0.0f))
        {
            vx += -direction * m_WalkSlowAcc * deltaTime;
            vx = direction > 0 ? Mathf.Max(vx, 0) : Mathf.Min(vx, 0);
        }
        else
        {
            vx += moveX * m_WalkAcc * deltaTime;
            if (vx > 0)
            {
                vx = Mathf.Min(vx, walkSpeed);
            }
            else if (vx < 0)
            {
                vx = Mathf.Max(vx, -walkSpeed);
            }
        }
        
        if (moveX >  Mathf.Epsilon && direction == DIR_LEFT ||
            moveX < -Mathf.Epsilon && direction == DIR_RIGHT)
        {
            Flip();
        }
        
        m_Velocity.x = vx;

        if (!dead && grounded)
        {
            animator.SetFloat("Speed", Mathf.Abs(m_Velocity.x));
            animator.SetBool("Stop", m_MoveInput.x > Mathf.Epsilon && m_Velocity.x < 0 || m_MoveInput.x < -Mathf.Epsilon && m_Velocity.x > 0);
        }
    }

    protected override void UpdatePosition(float deltaTime)
    {
        base.UpdatePosition(deltaTime);

        // keep player visible
        if (left < camera.left)
        {
            left = camera.left;
            m_Velocity.x = 0;
        }
        else if (right > camera.right)
        {
            right = camera.right;
            m_Velocity.x = 0;
        }
    }

    protected override void OnStartFalling()
    {
        if (!m_Jumping)
        {
            StartFall();
        }
    }

    protected override void OnStopFalling()
    {
        if (m_Jumping)
        {
            EndJump();
        }
        else
        {
            EndFall();
        }
    }

    protected override void OnObstacle(Cell cell)
    {
        m_Velocity.x = 0f;
    }

    protected override void OnJumpHitCell(Cell cell)
    {
        if (!cell.jumping)
        {
            cell.Hit(this);
        }
    }

    protected override void OnDie()
    {
        m_MoveInput = Vector2.zero;
        StartCoroutine(DieCoroutine());
    }

    #endregion

    #region State

    public void AdvanceState()
    {
        if (m_State < State.Super)
        {
            ChangeState(m_State + 1);
        }
    }

    internal void OnStartChangeStateAnimation()
    {
        Time.timeScale = 0; // stop everything when animation is played
    }

    internal void OnEndChangeStateAnimation()
    {
        Time.timeScale = 1; // resume everything

        if (m_Jumping)
        {
            animator.SetBool("Jump", true);
        }
    }

    void ChangeState(State state)
    {
        switch (state)
        {
            case State.Small:
                animator.runtimeAnimatorController = m_InitialAnimatorController;
                colliderRect = m_InitialColliderRect;
                StartInvincibility();
                break;
            case State.Big:
            case State.Super:
                animator.runtimeAnimatorController = m_BigAnimatorController;
                colliderRect = m_BigColliderRect;
                break;
        }

        m_State = state;
        animator.SetBool("Jump", false);
        animator.SetTrigger("ChangeState");
    }

    void StartInvincibility()
    {
        assert.IsFalse(m_Invincible);
        m_Invincible = true;

        StartCoroutine(InvincibilityCoroutine());
    }

    IEnumerator InvincibilityCoroutine()
    {
        assert.IsTrue(m_Invincible);

        SpriteRenderer renderer = GetRequiredComponent<SpriteRenderer>();
        Color currentColor = renderer.color;
        Color clearColor = Color.clear;

        for (int i = 0; i < 203; ++i) // magic number of frames
        {
            renderer.color = i % 2 == 0 ? clearColor : currentColor;
            yield return null;
        }

        renderer.color = currentColor;
        m_Invincible = false;
    }
    
    #endregion

    #region Jump

    void StartJump(float velocity)
    {
        if (!grounded)
        {
            EndFall();
        }

        m_Jumping = true;
        m_Velocity.y = velocity;

        assert.IsTrue(animator.enabled);
        animator.SetBool("Jump", true);
    }

    void EndJump()
    {
        m_Jumping = false;
        assert.IsTrue(animator.enabled);
        animator.SetBool("Jump", false);
    }

    void StartFall()
    {
        assert.IsFalse(grounded);
        assert.IsFalse(m_Jumping);
        animator.enabled = false;
    }

    void EndFall()
    {
        assert.IsFalse(m_Jumping);
        animator.enabled = true;
    }

    #endregion

    #region Death
    
    IEnumerator DieCoroutine()
    {
        animator.SetBool("Stop", false);
        animator.SetBool("Jump", false);
        animator.SetBool("Dead", true);

        movementEnabled = false;

        yield return new WaitForSeconds(0.25f);

        movementEnabled = true;
        m_Velocity.y = m_JumpHighSpeed; // FIXME: use anothe value

        yield return new WaitForSeconds(5);

        Destroy(gameObject);
    }

    #endregion

    #region Shot

    private void Shot()
    {
        var shotObject = Instantiate(m_shot.prefab) as FireBallController;
        shotObject.transform.parent = transform.parent;
        shotObject.transform.position = m_shot.origin.position;
        shotObject.Launch(direction);
    }

    #endregion

    #region Collisions

    protected override void OnCollision(LevelObjectСontroller other)
    {
        if (other.dead) return;

        if (bottom > other.bottom)
        {
            OnJumpOnObject(other);
        }
        else
        {
            OnCollideObject(other);
        }
    }

    void OnJumpOnObject(LevelObjectСontroller other)
    {
        EnemyController enemy = other as EnemyController;
        if (enemy != null)
        {
            enemy.OnPlayerJump(this);
            return;
        }

        Powerup powerup = other as Powerup;
        if (powerup != null)
        {
            PickPowerup(powerup);
        }
    }

    void OnCollideObject(LevelObjectСontroller other)
    {
        EnemyController enemy = other as EnemyController;
        if (enemy != null)
        {
            enemy.OnPlayerCollision(this);
            return;
        }

        Powerup powerup = other as Powerup;
        if (powerup != null)
        {
            PickPowerup(powerup);
        }
    }

    void PickPowerup(Powerup powerup)
    {
        powerup.Apply(this);
        Destroy(powerup.gameObject);
    }

    #endregion

    #region Damage

    protected override void OnDamage(LevelObjectСontroller attacker)
    {
        if (m_State == State.Small)
        {
            Die();
        }
        else
        {
            ChangeState(State.Small);
        }
    }

    #endregion

    #region Enemies

    public void JumpOnEnemy(EnemyController enemy)
    {
        StartCoroutine(JumpOnEnemyCoroutine(enemy));
    }

    IEnumerator JumpOnEnemyCoroutine(EnemyController enemy)
    {
        float bottomTargetY = enemy.posY;
        
        // player's bottom should be at the center of an enemy
        while (bottom > bottomTargetY)
        {
            yield return null;
        }
        
        bottom = bottomTargetY;
        m_Velocity.y = m_JumpSquashSpeed;
    }

    #endregion

    #region Properties

    public bool invincible
    {
        get { return m_Invincible; }
    }

    public bool isSmall
    {
        get { return m_State == State.Small; }
    }

    #endregion
}