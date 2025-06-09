using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimatorController : MonoBehaviour
{
    private Animator animator;

    // Animator parameter names
    private const string IsGroundedParam = "IsGrounded";
    private const string RunningParam = "Running";
    private const string JumpingParam = "Jumping";

    // 内部状态跟踪
    private bool isJumping = false;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void SetGrounded(bool isGrounded)
    {
        if (animator != null)
        {
            animator.SetBool(IsGroundedParam, isGrounded);
        }
    }

    public void SetRunning(bool isRunning)
    {
        if (animator != null)
        {
            animator.SetBool(RunningParam, isRunning);
        }
    }

    public void SetJumping(bool jumping)
    {
        isJumping = jumping; // 跟踪内部状态
        if (animator != null)
        {
            animator.SetBool(JumpingParam, jumping);
        }
    }

    // 获取当前跳跃状态
    public bool GetJumpingState()
    {
        return isJumping;
    }
}
