using System.Collections;
using UnityEngine;

/// <summary>
/// 2D 효과음 재생 매니저(싱글턴). 위치와 무관하게 잘 들리는 2D 사운드.
/// 각 클립/볼륨은 Inspector에서 지정·교체 가능하며, 같은 소리가 한 프레임에
/// 여러 번 겹쳐 터지지 않도록 최소 간격 가드를 둔다.
/// 게임 로직은 건드리지 않고, 이벤트 지점에서 Play* 를 호출만 한다.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Source")]
    [Tooltip("2D 효과음 재생용 AudioSource. 비우면 자동 생성.")]
    [SerializeField] private AudioSource sfxSource;

    [Header("Clips")]
    [SerializeField] private AudioClip shootClip;    // soccer-ball-kick
    [SerializeField] private AudioClip hitClip;       // punch-impact-hit
    [SerializeField] private AudioClip wallClip;      // impact-ball
    [SerializeField] private AudioClip whistleClip;   // referee-whistle
    [SerializeField] private AudioClip cheerClip;     // crowd-cheer

    [Header("Volumes")]
    [Range(0f,1f)] [SerializeField] private float masterVolume = 1f;
    [Range(0f,1f)] [SerializeField] private float shootVolume = 0.9f;
    [Range(0f,1f)] [SerializeField] private float hitVolume = 1f;
    [Range(0f,1f)] [SerializeField] private float wallVolume = 0.7f;
    [Range(0f,1f)] [SerializeField] private float whistleVolume = 0.85f;
    [Range(0f,1f)] [SerializeField] private float cheerVolume = 0.9f;

    [Header("Goal Sequence")]
    [Tooltip("골 시 휘슬 후 환호를 재생하기까지의 지연(초).")]
    [SerializeField] private float cheerDelay = 0.6f;

    [Header("Anti-Spam")]
    [Tooltip("같은 소리의 최소 재생 간격(초).")]
    [SerializeField] private float minRepeatInterval = 0.05f;
    [Tooltip("벽 튕김 소리 최소 간격(초) — 너무 자주 나지 않게.")]
    [SerializeField] private float wallMinInterval = 0.12f;

    private float lastShoot = -99f, lastHit = -99f, lastWall = -99f, lastGoal = -99f;

    private void Awake()
    {
        Instance = this;
        if (sfxSource == null) sfxSource = GetComponent<AudioSource>();
        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f; // 2D
    }

    private bool Ready(AudioClip clip, ref float last, float interval)
    {
        if (clip == null || sfxSource == null) return false;
        if (Time.unscaledTime - last < interval) return false;
        last = Time.unscaledTime;
        return true;
    }

    public void PlayShoot()
    {
        if (Ready(shootClip, ref lastShoot, minRepeatInterval))
            sfxSource.PlayOneShot(shootClip, shootVolume * masterVolume);
    }

    public void PlayHit()
    {
        if (Ready(hitClip, ref lastHit, minRepeatInterval))
            sfxSource.PlayOneShot(hitClip, hitVolume * masterVolume);
    }

    public void PlayWallBounce()
    {
        if (Ready(wallClip, ref lastWall, wallMinInterval))
            sfxSource.PlayOneShot(wallClip, wallVolume * masterVolume);
    }

    /// <summary>골: 휘슬 재생 후 이어서 환호 재생.</summary>
    public void PlayGoal()
    {
        if (Time.unscaledTime - lastGoal < 0.3f) return; // 중복 방지
        lastGoal = Time.unscaledTime;

        if (whistleClip != null && sfxSource != null)
            sfxSource.PlayOneShot(whistleClip, whistleVolume * masterVolume);
        StartCoroutine(PlayCheerAfterDelay());
    }

    private IEnumerator PlayCheerAfterDelay()
    {
        yield return new WaitForSeconds(cheerDelay);
        if (cheerClip != null && sfxSource != null)
            sfxSource.PlayOneShot(cheerClip, cheerVolume * masterVolume);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
