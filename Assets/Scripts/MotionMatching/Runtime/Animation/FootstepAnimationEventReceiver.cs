using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class FootstepAnimationEventReceiver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip[] defaultFootstepClips;
    [SerializeField] private AudioClip[] leftFootstepClips;
    [SerializeField] private AudioClip[] rightFootstepClips;

    [Header("Sound Settings")]
    [SerializeField] private float volume = 0.75f;
    [SerializeField] private float pitchRandomness = 0.08f;
    [SerializeField] private float minTimeBetweenFootsteps = 0.16f;
    [SerializeField] private float duplicateFootstepBlockTime = 0.14f;
    [SerializeField] private bool stopPreviousFootstepBeforePlaying = true;

    [Header("Animator Transition Guard")]
    [SerializeField] private int animatorLayerIndex = 0;
    [SerializeField] private bool blockFootstepsDuringAnimatorTransition = true;
    [SerializeField] private float resetDelayAfterStateChange = 0.08f;

    [Header("Optional Animation Curve Volume")]
    [SerializeField] private bool useAnimatorFootstepVolume = false;
    [SerializeField] private string footstepVolumeParameter = "FootstepVolume";

    [Header("Ground Check")]
    [SerializeField] private bool requireGroundedForSound = true;
    [SerializeField] private Transform groundCheckOrigin;
    [SerializeField] private float groundCheckDistance = 0.8f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Debug")]
    [SerializeField] private bool debugEvents = false;

    private float lastFootstepTime;
    private float blockedUntilTime;
    private float originalPitch;
    private int footstepVolumeHash;
    private PlayerAnimator playerAnimator;

    private void Awake()
    {
        playerAnimator = GetComponent<PlayerAnimator>();
        if (animator == null)
            animator = GetComponent<Animator>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
        {
            originalPitch = audioSource.pitch;
            audioSource.playOnAwake = false;
        }

        footstepVolumeHash = Animator.StringToHash(footstepVolumeParameter);
    }

    public void PlayFootstep()
    {
        PlayFootstepFromClips(defaultFootstepClips);
    }

    public void PlayLeftFootstep()
    {
        if (leftFootstepClips != null && leftFootstepClips.Length > 0)
            PlayFootstepFromClips(leftFootstepClips);
        else
            PlayFootstepFromClips(defaultFootstepClips);
    }

    public void PlayRightFootstep()
    {
        if (rightFootstepClips != null && rightFootstepClips.Length > 0)
            PlayFootstepFromClips(rightFootstepClips);
        else
            PlayFootstepFromClips(defaultFootstepClips);
    }

    public void ResetFootstepGate()
    {
        blockedUntilTime = Time.time + resetDelayAfterStateChange;
        lastFootstepTime = Time.time;

        if (stopPreviousFootstepBeforePlaying && audioSource != null)
            audioSource.Stop();
    }

    private void PlayFootstepFromClips(AudioClip[] clips)
    {
        if (audioSource == null)
            return;

        if (clips == null || clips.Length == 0)
            return;

        if (Time.time < blockedUntilTime)
        {
            if (debugEvents)
                Debug.Log("Footstep blocked: reset delay after state change.", this);

            return;
        }

        float requiredFootstepGap = Mathf.Max(minTimeBetweenFootsteps, duplicateFootstepBlockTime);

        if (Time.time - lastFootstepTime < requiredFootstepGap)
        {
            if (debugEvents)
                Debug.Log("Footstep blocked: too close to previous footstep.", this);

            return;
        }

        if (blockFootstepsDuringAnimatorTransition && animator != null)
        {
            if (playerAnimator != null ? playerAnimator.IsTransitioning(animatorLayerIndex) : animator.IsInTransition(animatorLayerIndex))
            {
                blockedUntilTime = Mathf.Max(blockedUntilTime, Time.time + resetDelayAfterStateChange);

                if (debugEvents)
                    Debug.Log("Footstep blocked: Animator is in transition.", this);

                return;
            }
        }

        if (requireGroundedForSound && !IsGroundedForFootstep())
        {
            if (debugEvents)
                Debug.Log("Footstep blocked: ground check failed.", this);

            return;
        }

        AudioClip clip = clips[Random.Range(0, clips.Length)];

        if (clip == null)
            return;

        lastFootstepTime = Time.time;

        float finalVolume = volume;

        if (useAnimatorFootstepVolume && animator != null)
        {
            finalVolume *= Mathf.Clamp01(playerAnimator != null ? playerAnimator.ReadFloat(footstepVolumeHash) : animator.GetFloat(footstepVolumeHash));
        }

        audioSource.pitch = Random.Range(
            originalPitch - pitchRandomness,
            originalPitch + pitchRandomness
        );

        if (stopPreviousFootstepBeforePlaying)
            audioSource.Stop();

        audioSource.clip = clip;
        audioSource.volume = finalVolume;
        audioSource.Play();

        if (debugEvents)
            Debug.Log("Footstep played: " + clip.name, this);
    }

    private bool IsGroundedForFootstep()
    {
        Vector3 origin = groundCheckOrigin != null ? groundCheckOrigin.position : transform.position;

        return Physics.Raycast(
            origin,
            Vector3.down,
            groundCheckDistance,
            groundLayer,
            QueryTriggerInteraction.Ignore
        );
    }

    private void OnDrawGizmos()
    {
        Vector3 origin = groundCheckOrigin != null ? groundCheckOrigin.position : transform.position;
        Vector3 end = origin + Vector3.down * groundCheckDistance;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, end);
        Gizmos.DrawWireSphere(end, 0.05f);
    }
}
