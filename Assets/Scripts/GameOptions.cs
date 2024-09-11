using UnityEngine;

public enum GameMode
{
    Normal,
    Degree90,
    Degree180,
    Degree270,
    Random,
    Random2,
    HalfRandom,
    FullRandom // 무리배치(겹노트) 해결해야함
}

public enum JudgementVisibility
{
    None,
    Text,
    Number
}

public enum JudgementType
{
    Normal,
    Hard,
    Extreme
}

public class GameOptions : MonoBehaviour
{
    public static GameOptions Instance { get; private set; }

    public float MasterVolume { get; set; } = 1f;
    public float ClapVolume { get; set; }
    public float MusicVolume { get; set; } = .35f;
    public GameMode Mode { get; set; } = GameMode.Normal;
    public JudgementVisibility JudgementVisibilityType { get; set; } = JudgementVisibility.Number;
    public bool AutoPlay
    {
        get => autoPlay; 
        private set
        {
            if (GameManager.Instance.StartTime <= 0)
            {
                autoPlay = value;
            }
        }
    };
    public JudgementType JudgementType
    {
        get => judgementType;
        set
        {
            if (GameManager.Instance.StartTime <= 0)
            {
                judgementType = value;
            }
        }
    }

    private bool autoPlay;
    private JudgementType judgementType = JudgementType.Normal;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        // TODO: setting.json 읽기
        Instance = this;
        DontDestroyOnLoad(gameObject);

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;

        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Screen.SetResolution(Screen.height * 10 / 16, Screen.height, true);
    }
}
