using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

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

public enum JudgementType
{
    Normal,
    Hard,
    Extreme
}

public enum JudgementVisibilityType
{
    None,
    Text,
    Number
}

public enum MusicSortMethod
{
    Name,
    Artist,
    Score
}

public enum SortType
{
    Ascending,
    Descending
}

public class GameOptions : MonoBehaviour
{
    public static GameOptions Instance { get; private set; }
    public float MusicVolume { get; set; } = .35f;
    public GameMode GameMode
    {
        get => gameMode;
        set
        {
            gameMode = value;
            var dropdown = UIManager.Instance.GetUIObject<Dropdown>("RandomSettings");
            if (dropdown != null)
            {
                dropdown.value = (int) value;
            }
        }
    }
    public JudgementType JudgementType
    {
        get => judgementType;
        set
        {
            judgementType = value;
            var dropdown = UIManager.Instance.GetUIObject<Dropdown>("JudgeSettings");
            if (dropdown != null)
            {
                dropdown.value = (int) value;
            }
        }
    }

    public bool ShowJudgementState { get; set; } = true;

    public bool AutoPlay
    {
        get => autoPlay; 
        set
        {
            autoPlay = value;
            var button = UIManager.Instance.GetUIObject<Button>("AutoPlay");
            if (button != null)
            {
                button.GetComponentInChildren<Text>().text = $"자동: {(value ? "켜짐" : "꺼짐")}";
            }
        }
    }
    public bool AutoClap
    {
        get => autoClap;
        set
        {
            autoClap = value;
            var button = UIManager.Instance.GetUIObject<Button>("AutoClap");
            if (button != null)
            {
                button.GetComponentInChildren<Text>().text = $"박수: {(value ? "켜짐" : "꺼짐")}";
            }
        }
    }

    public MusicSortMethod MusicSortMethod
    {
        get => musicSortMethod;
        set
        {
            musicSortMethod = value;
            // TODO: UI 업데이트
        }
    }

    public SortType SortType
    {
        get => sortType;
        set
        {
            sortType = value;
            // TODO: UI 업데이트
        }
    }

    private bool autoPlay;
    private bool autoClap;
    private GameMode gameMode = GameMode.Normal;
    private SortType sortType = SortType.Ascending;
    private JudgementType judgementType = JudgementType.Normal;
    private MusicSortMethod musicSortMethod = MusicSortMethod.Name;

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

    private void Start()
    {
        var config = LoadConfig();
        // Game Style Setting
        if (Enum.TryParse<GameMode>(config.GetValueOrDefault("gamemode", ""), true, out var mode))
        {
            GameMode = mode;
        }
        if (Enum.TryParse<JudgementType>(config.GetValueOrDefault("judgement_type", ""), true, out var type))
        {
            JudgementType = type;
        }

        // Visual Setting
        if (bool.TryParse(config.GetValueOrDefault("show_judgement_state", "false"), out var visibility))
        {
            ShowJudgementState = visibility;
        }

        // Volume Setting
        if (float.TryParse(config.GetValueOrDefault("master_volume", ""), out var volume))
        {
            AudioListener.volume = Mathf.Clamp01(volume);
        }
        if (float.TryParse(config.GetValueOrDefault("music_volume", ""), out volume))
        {
            MusicVolume = Mathf.Clamp01(volume);
        }

        // Auto Play Setting
        if (bool.TryParse(config.GetValueOrDefault("auto_play", "false"), out var auto))
        {
            AutoPlay = auto;
        }
        if (bool.TryParse(config.GetValueOrDefault("clap_sound", "false"), out auto))
        {
            AutoClap = auto;
        }
    }

    private Dictionary<string, string> LoadConfig()
    {
        var filePath = Path.Combine(Application.dataPath, "..", "data", "config.properties");
        Dictionary<string, string> properties = new();
        if (!File.Exists(filePath))
        {
            return properties;
        }

        // 파일에서 한 줄씩 읽어들임
        foreach (var line in File.ReadAllLines(filePath))
        {
            // 주석 처리 (#)이거나 빈 줄은 무시
            if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line)) continue;
            var keyValue = line.Split("=", 2);
            if (keyValue.Length < 2) continue;
            properties[keyValue[0].Trim()] = keyValue[1].Trim();
        }

        return properties;
    }

    private void SaveConfig()
    {
        var filePath = Path.Combine(Application.dataPath, "..", "data", "config.properties");
        var textList = new[]
        {
            "# Game Style",
            $"gamemode={GameMode}",
            $"judgement_type={JudgementType}",
            "",
            "# Visual",
            $"show_judgement_state={ShowJudgementState}",
            "",
            "# Game Volume",
            $"master_volume={AudioListener.volume}",
            $"music_volume={MusicVolume}",
            "",
            "# Auto Play",
            $"auto_play={AutoPlay}",
            $"clap_sound={AutoClap}",
        };
        File.WriteAllLines(filePath, textList);
    }

    private void OnApplicationQuit()
    {
        SaveConfig();
    }
}
