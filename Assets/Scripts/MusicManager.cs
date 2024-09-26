using System;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public enum Difficulty
{
    Basic,
    Advanced,
    Extreme
}

[Serializable]
public class MusicInfo
{
    public string title;
    public string artist;
    public float offset;
    public float preview;
    public float duration;
}

[Serializable]
public class MusicScoreData
{
    public int score;
    public List<int> musicBar = new();

}

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    public Sprite DefaultJacket;

    public List<Music> MusicList { get; } = new();

    private AudioType GetAudioType(string extension)
    {
        return extension.ToLower() switch
        {
            ".mp3" => AudioType.MPEG,
            ".ogg" => AudioType.OGGVORBIS,
            ".wav" => AudioType.WAV,
            _ => AudioType.UNKNOWN,
        };
    }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        LoadMusicDir();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        switch (scene.name)
        {
            case GameManager.SceneMusicSelect:
                UIManager.Instance.ResetMusicList();
                foreach (var music in MusicList)
                {
                    UIManager.Instance.AddMusicButton(music);
                }
                break;
        }
    }

    public void Sort()
    {
        switch (GameOptions.Instance.MusicSortMethod)
        {
            case MusicSortMethod.Title:
                if (GameOptions.Instance.MusicSortType == SortType.Ascending)
                    MusicList.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase));
                else
                    MusicList.Sort((a, b) => string.Compare(b.Title, a.Title, StringComparison.OrdinalIgnoreCase));
                break;
            case MusicSortMethod.Artist:
                if (GameOptions.Instance.MusicSortType == SortType.Ascending)
                    MusicList.Sort((a, b) => string.Compare(a.Artist, b.Artist, StringComparison.OrdinalIgnoreCase));
                else
                    MusicList.Sort((a, b) => string.Compare(b.Artist, a.Artist, StringComparison.OrdinalIgnoreCase));
                break;
            default:
                var difficulty = GameManager.Instance.CurrentDifficulty;
                if (GameOptions.Instance.MusicSortType == SortType.Ascending)
                    MusicList.Sort((a, b) => a.GetScore(difficulty).CompareTo(b.GetScore(difficulty)));
                else
                    MusicList.Sort((a, b) => b.GetScore(difficulty).CompareTo(a.GetScore(difficulty)));
                break;
        }

        UIManager.Instance.ResetMusicList();
        foreach (var music in MusicList)
        {
            UIManager.Instance.AddMusicButton(music);
        }
    }

    public void LoadMusicDir()
    {
        var basePath = Path.Combine(Application.dataPath, "..", "Songs");
        if (!Directory.Exists(basePath))
        {
            return;
        }

        var scorePath = Path.Combine(Application.dataPath, "..", "data", "score.json");
        Dictionary<string, Dictionary<string, MusicScoreData>> scoreDataList;
        if (File.Exists(scorePath))
        {
            var text = File.ReadAllText(scorePath);
            scoreDataList = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, MusicScoreData>>>(text);
        }
        else
        {
            scoreDataList = new();
        }

        var allFiles = Directory.GetDirectories(basePath);
        var currentCount = 0;
        var totalCount = allFiles.Length;
        UIManager.Instance.ResetMusicList();
        foreach (var dirPath in allFiles)
        {
            StartCoroutine(LoadMusic(dirPath, () =>
            {
                ++currentCount;
                if (totalCount > currentCount) return;
                foreach (var (musicName, difficultyTable) in scoreDataList)
                {
                    var music = MusicList.Find(music =>
                    {
                        var nameSplit = musicName.Split(" [HOLD]");
                        var isLongData = nameSplit.Length > 1;
                        if ((isLongData && !music.IsLong) || (!isLongData && music.IsLong)) return false;
                        return music.Title == nameSplit[0];
                    });
                    if (music == null) continue;
                    foreach (var (difficultyStr, scoreData) in difficultyTable)
                    {
                        if (!Enum.TryParse(difficultyStr, true, out Difficulty difficulty)) continue;
                        music.SetScore(difficulty, scoreData.score);
                        music.SetMusicBarScore(difficulty, scoreData.musicBar);
                    }
                }
                Sort();
                foreach (var music in MusicList)
                {
                    List<string> notExists = new();
                    if (music.Artist == "작곡가")
                    {
                        notExists.Add("작곡가");
                    }
                    if (music.Jacket == DefaultJacket)
                    {
                        notExists.Add("자켓");
                    }
                    if (music.Offset == 0)
                    {
                        notExists.Add("싱크 조절");
                    }

                    if (notExists.Count > 0)
                    {
                        Debug.Log($"{music.Title}({music.Artist})에 없는것: [{string.Join(", ", notExists)}]");
                    }
                }
            }));
        }
    }

    private IEnumerator LoadMusic(string dirPath, Action afterFunction)
    {
        var dirName = Path.GetFileName(dirPath);
        var songFiles = Directory.GetFiles(dirPath, "song.*");
        if (songFiles.Length < 1)
        {
            Debug.LogWarning($"'{dirName}' 폴더엔 음악 파일이 존재하지 않습니다.");
            afterFunction();
            yield break;
        }

        var musicPath = songFiles[0];
        var audioType = GetAudioType(Path.GetExtension(musicPath));
        if (audioType == AudioType.UNKNOWN)
        {
            Debug.LogWarning($"'{dirName}' 폴더엔 음악 파일이 존재하지 않습니다.");
            afterFunction();
            yield break;
        }

        using var www = UnityWebRequestMultimedia.GetAudioClip("file://" + musicPath, audioType);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"폴더: {musicPath}, 오류: {www.error}");
            afterFunction();
            yield break;
        }

        var sprite = DefaultJacket;
        var jacketFiles = Directory.GetFiles(dirPath, "jacket.*");
        if (jacketFiles.Length > 0)
        {
            var fileData = File.ReadAllBytes(jacketFiles[0]);
            Texture2D texture = new(2, 2);
            if (texture.LoadImage(fileData))
            {
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
        }

        Music music = null;
        var clip = DownloadHandlerAudioClip.GetContent(www);
        var jsonPath = Path.Combine(dirPath, "info.json");
        if (File.Exists(jsonPath))
        {
            try
            {
                var json = File.ReadAllText(jsonPath);
                var musicInfo = JsonUtility.FromJson<MusicInfo>(json);
                musicInfo.title ??= dirName;
                musicInfo.artist ??= "작곡가";
                music = new(clip, dirPath, musicInfo, sprite);
            }
            catch
            {
                Debug.LogError($"{dirName} 폴더 내의 info.json 파일이 잘못되었습니다.");
            }
        }

        var success = false;
        music ??= new(clip, dirPath, dirName, sprite);
        for (int i = 0, limit = (int)Difficulty.Extreme; i <= limit; ++i)
        {
            var difficulty = (Difficulty) i;
            var chart = Chart.Parse(music, difficulty);
            if (chart == null) continue;
            success = true;
            music.AddChart(chart);
        }

        if (music.IsValid)
        {
            MusicList.Add(music);
        }

        var ver2Files = Directory.GetFiles(dirPath, "*_2.txt");
        if (ver2Files.Length > 0)
        {
            var music2 = music.Clone();
            for (int i = 0, limit = (int)Difficulty.Extreme; i <= limit; ++i)
            {
                var difficulty = (Difficulty)i;
                var chart = Chart.Parse(music2, difficulty, true);
                if (chart == null) continue;
                success = true;
                music2.AddChart(chart);
            }
            if (music2.IsValid)
            {
                MusicList.Add(music2);
            }
        }

        if (!success)
        {   
            Debug.LogWarning($"{music.Title}({music.Artist})에는 채보가 존재하지 않습니다.");
        }
        afterFunction();
    }
}

public class Music{
    public readonly string Title;
    public readonly string Artist = "작곡가";
    public readonly float Preview = 35f;
    public readonly float Duration = 10f;
    /**
    * 음악이 시작되는 시간
    * 값이 작아지면: 노래가 빨리재생됨(노래가 느릴때 이쪽으로)
    * 값이 커지면: 노래가 늦게재생됨(노래가 빠를때 이쪽으로)
    */
    public float Offset
    {
        get => offset;
        set
        {
            offset = value;
            foreach (var item in chartList)
            {
                item.Value.CreateMusicBar();
            }
        }
    }

    public readonly string MusicPath;
    public readonly AudioClip Clip;
    public readonly Sprite Jacket;
    public readonly Dictionary<Difficulty, int> ScoreList = new()
    {
        { Difficulty.Basic, 0 },
        { Difficulty.Advanced, 0 },
        { Difficulty.Extreme, 0 }
    };
    public readonly Dictionary<Difficulty, List<int>> MusicBarScoreList = new()
    {
        { Difficulty.Basic, new(new int[120]) },
        { Difficulty.Advanced, new(new int[120]) },
        { Difficulty.Extreme, new(new int[120]) }
    };

    public bool IsValid => chartList.Count > 0;

    public bool IsLong { get; private set; }

    private float offset = 0;
    private readonly Dictionary<Difficulty, Chart> chartList = new();

    public Music(AudioClip clip, string musicPath, string title, Sprite jacket)
    {
        Clip = clip;
        MusicPath = musicPath;
        Title = title;
        Jacket = jacket;
    }

    public Music(AudioClip clip, string musicPath, MusicInfo info, Sprite jacket)
    {
        Clip = clip;
        MusicPath = musicPath;
        Jacket = jacket;

        Title = info.title;
        Artist = info.artist;
        Offset = info.offset;
        if (0 <= info.preview && info.preview < clip.length)
        {
            Preview = info.preview;
        }
        if (0 <= info.duration && Preview + info.duration <= clip.length)
        {
            Duration = info.duration;
        }
    }

    public Music Clone()
    {
        MusicInfo info = new()
        {
            title = Title,
            artist = Artist,
            offset = Offset,
            preview = Preview,
            duration = Duration
        };
        return new(Clip, MusicPath, info, Jacket);
    }

    public void AddChart(Chart chart)
    {
        if (chartList.TryAdd(chart.Difficulty, chart))
        {
            IsLong = IsLong || chart.IsLong;
        }
    }

    public Chart GetChart(Difficulty difficulty)
    {
        return chartList.GetValueOrDefault(difficulty);
    }

    public bool CanPlay(Difficulty difficulty)
    {
        return chartList.ContainsKey(difficulty);
    }

    public int GetScore(Difficulty difficulty)
    {
        return ScoreList[difficulty];
    }

    public List<int> GetMusicBarScore(Difficulty difficulty)
    {
        return MusicBarScoreList[difficulty];
    }

    public void SetScore(Difficulty difficulty, int score)
    {
        if (ScoreList[difficulty] < score)
        {
            ScoreList[difficulty] = score;
        }
    }

    public void SetMusicBarScore(Difficulty difficulty, List<int> score)
    {
        for (int i = 0, length = Math.Min(score.Count, 120); i < length; ++i)
        {
            MusicBarScoreList[difficulty][i] = Math.Max(MusicBarScoreList[difficulty][i], score[i]);
        }
    }

    public async void SaveInfo()
    {
        var filePath = Path.Combine(MusicPath, "info.json");
        Dictionary<string, object> json = new()
        {
            {"title", Title},
            {"artist", Artist},
            {"offset", Offset},
            {"preview", Preview},
            {"duration", Duration},
        };
        var jsonStr = JsonConvert.SerializeObject(json, Formatting.Indented);
        await File.WriteAllTextAsync(filePath, jsonStr);
    }

    public async void SaveScore(Difficulty difficulty)
    {
        Dictionary<string, Dictionary<string, MusicScoreData>> json;
        var scorePath = Path.Combine(Application.dataPath, "..", "data", "score.json");
        if (File.Exists(scorePath))
        {
            var text = await File.ReadAllTextAsync(scorePath);
            json = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, MusicScoreData>>>(text);
        }
        else
        {
            json = new();
            await Task.Run(() => Directory.CreateDirectory(Path.GetDirectoryName(scorePath)));
        }

        var title = $"{Title}{(IsLong ? " [HOLD]" : "")}";
        json.TryAdd(title, new());
        json[title][difficulty.ToString()] = new()
        {
            score = GetScore(difficulty),
            musicBar = GetMusicBarScore(difficulty)
        };
        var jsonStr = JsonConvert.SerializeObject(json);
        await File.WriteAllTextAsync(scorePath, jsonStr);
    }
}

public class Chart
{
    //口 or □ 빈칸, ① ~ ⑳(０ ~ ９)Ａ ~ Ｚ 노트 위치
    public static readonly Regex NoteRegex = new(@"^[口□①-⑳┼｜┃━―←↑↓→＜∧∨＞<^V>０-９Ａ-Ｚ]{4}$", RegexOptions.Compiled);
    public static readonly Regex NoteTimingRegex = new(@"^[口□①-⑳┼｜┃━―←↑↓→＜∧∨＞<^V>０-９Ａ-Ｚ]{4}\|.+(\|)?$", RegexOptions.Compiled);

    public readonly Music Music;
    public readonly double Level;
    public readonly Difficulty Difficulty;

    /** BPM 목록 */
    public readonly List<double> BpmList = new();
    /** 모든 박자가 들어가는 배열 */
    public readonly SortedSet<double> ClapTimings = new();

    public int NoteCount { get; private set; }
    public bool IsLong { get; private set; }
    public string BpmString
    {
        get
        {
            var min = BpmList.Min();
            var max = BpmList.Max();
            return Math.Abs(max - min) < 0.01 ? min + "" : $"{min}-{max}";
        }
    }
    public string LevelString => Level >= 9 ? Level.ToString("F1") : Level + "";
    public List<Note> NoteList
    {
        get
        {
            allNotes ??= gridNoteList.SelectMany(pair => pair.Value).OrderBy(note => note.StartTime).ToList();
            return allNotes;
        }
    }

    /** 모든 노트의 출현 순서별 정렬 */
    private List<Note> allNotes;

    /**
     * 그리드 별 노트 배열 [row * 4 + column] = List<Note /> 
     */
    private readonly Dictionary<int, List<Note>> gridNoteList = new();

    private List<int> musicBar;
    public List<int> MusicBar => musicBar ?? CreateMusicBar();

    public int Score => Music.GetScore(Difficulty);
    public List<int> MusicBarScore => Music.GetMusicBarScore(Difficulty);

    private Chart(Music music, double level, Difficulty difficulty)
    {
        Music = music;
        Level = level > 8 ? (int) (level * 10) / 10d : (int) level;
        Difficulty = difficulty;
    }

    private static bool TryParseDoubleInText(string text, out double result)
    {
        var match = Regex.Match(text, @"-?\d+(\.\d+)?");
        return double.TryParse(match.Success ? match.Value : "", out result);
    }

    private static string RemoveComment(string text)
    {
        var commentIndex = text.IndexOf("//", StringComparison.Ordinal);
        return (commentIndex > 0 ? text[..commentIndex].Trim() : text.Trim()).Replace(" ", "");
    }

    public static bool IsNoteText(string text)
    {
        return NoteRegex.IsMatch(text) || NoteTimingRegex.IsMatch(text);
    }

    public static bool IsBpmText(string text)
    {
        var lower = text.ToLower();
        return lower.StartsWith("bpm") || lower.StartsWith("t=") || lower.StartsWith("#t=");
    }

    public static Chart Parse(Music music, Difficulty difficulty, bool ver2 = false)
    {
        var diffStr = difficulty.ToString().ToLower();
        var filePath = Path.Combine(music.MusicPath, $"{diffStr}{(ver2 ? "_2" : "")}.txt");
        if (!File.Exists(filePath))
        {
            return null;
        }

        var level = 1.0;
        var lines = File.ReadAllLines(filePath);
        foreach (var text in lines)
        {
            var line = RemoveComment(text).ToLower();
            if ((line.StartsWith("#lev") || line.StartsWith("lev")) && TryParseDoubleInText(line, out level))
            {
                break;
            }
        }
        var startOffset = 0.0;
        Chart chart = new(music, level, difficulty);
        for (var i = 0; i < lines.Length; ++i)
        {
            var line = RemoveComment(lines[i]);
            if (line.Length < 1)
            {
                continue;
            }

            if (IsBpmText(line) && TryParseDoubleInText(line, out var bpmValue))
            {
                chart.BpmList.Add(bpmValue);
            }
            else if (IsNoteText(line))
            {
                var j = -1;
                var count = 0;
                var chartPart = new ChartPart(startOffset, chart);
                while (lines.Length > i + ++j)
                {
                    var noteLine = RemoveComment(lines[i + j]);
                    if (noteLine.Length < 1)
                    {
                        continue;
                    }

                    var isNoteText = NoteRegex.IsMatch(noteLine);
                    var isNoteTimingText = NoteTimingRegex.IsMatch(noteLine);
                    if (!isNoteText && !isNoteTimingText) // BPM 혹은 (명시된) 다음 마디가 나온경우
                    {
                        if (count % 4 == 0) // 잘 종료되었음
                        {
                            --j;
                            break;   
                        }
                        Debug.LogError($"{music.Title}({music.Artist})의 {difficulty} 채보 형식이 잘못되었습니다.\n{i + j + 1}줄의 내용: {noteLine}");
                        return null;
                    }

                    if (count > 0 && count % 4 == 0 && isNoteTimingText) // 4칸을 읽은뒤 ㅁㅁㅁㅁ|----| 형태가 나온다면 마디구분이 없더라도 새 마디로 취급
                    {
                        --j;
                        break;
                    }

                    ++count;
                    var gridPart = noteLine[..4];
                    chartPart.AddNotePositionText(gridPart);

                    var timingSplit = noteLine[4..].Trim().Split("|");
                    if (timingSplit.Length <= 1) continue;
                    chartPart.NoteTimings.Add(timingSplit[1].Trim());
                }
                chartPart.Convert();
                i += j;
                startOffset = chartPart.StartOffset;
            }
        }
        return chart.NoteCount < 1 ? null : chart;
    }

    public void AddNote(Note newNote)
    {
        if (newNote.IsLong)
        {
            IsLong = true;
        }

        var index = newNote.Row * 4 + newNote.Column;
        if (!gridNoteList.TryGetValue(index, out var noteList))
        {
            gridNoteList.Add(index, new List<Note> { newNote });
        }
        else
        {
            var lastNote = noteList[^1];
            /*var lastTime = lastNote.IsLong ? lastNote.FinishTime : lastNote.StartTime;
            if (newNote.StartTime - lastTime <= 31 / 30d)
            {
                Debug.Log($"[{Music.Title}] 충돌날 수 있는 노트 발견됨. 이전노트: {lastTime}, 다음노트: {newNote.StartTime}, Row: {newNote.Row}, Col: {newNote.Column}");
            }*/

            if (lastNote.IsLong && lastNote.FinishTime <= 0)
            {
                lastNote.FinishTime = newNote.StartTime;
            }
            else
            {
                noteList.Add(newNote);
            }
        }
        ++NoteCount;
        ClapTimings.Add(newNote.StartTime);
    }

    public List<int> CreateMusicBar()
    {
        musicBar = new(new int[120]);
        var limit = musicBar.Count;
        var divide = Music.Clip.length / limit;
        var offset = 29 / 60d - Music.Offset;
        foreach (var note in NoteList)
        {
            var startBarIndex = (int) Math.Floor((note.StartTime + offset) / divide);
            musicBar[startBarIndex]++;
            note.MusicBarIndex = startBarIndex;

            if (!(note.FinishTime > 0)) continue;
            var finishBarIndex = (int)Math.Floor((note.FinishTime + offset) / divide);
            musicBar[finishBarIndex]++;
            note.MusicBarLongIndex = finishBarIndex;
        }
        return musicBar;
    }
}

public class ChartRandomHelper
{
    private readonly List<Note> noteList;
    private readonly Dictionary<(int, int), List<(double, double)>> occupiedTimeGrid = new();
    private readonly Dictionary<(int, int), List<(double, double)>> occupiedTimeGridArrow = new();

    public ChartRandomHelper(List<Note> noteList)
    {
        this.noteList = noteList;
    }

    private void ShuffleList(List<int> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private List<Note> Random()
    {
        List<int> row = new();
        List<int> column = new();
        while (row.Count < 4)
        {
            var random = UnityEngine.Random.Range(0, 4);
            if (!row.Contains(random))
            {
                row.Add(random);
            }
        }
        while (column.Count < 4)
        {
            var random = UnityEngine.Random.Range(0, 4);
            if (!column.Contains(random))
            {
                column.Add(random);
            }
        }
        return noteList.Select(note => note.Random(row, column)).ToList();
    }

    private List<Note> RandomPlus()
    {
        List<int> position = new();
        while (position.Count < 16)
        {
            var random = UnityEngine.Random.Range(0, 16);
            if (!position.Contains(random))
            {
                position.Add(random);
            }
        }
        return noteList.Select(note => note.Random(position)).ToList();
    }

    private List<Note> HalfRandom()
    {
        // 왼쪽 영역과 오른쪽 영역의 위치를 각각 담을 리스트를 생성합니다.
        List<int> leftPositions = new();
        List<int> rightPositions = new();

        // 16개의 위치를 순회하며 영역에 따라 분류합니다.
        for (var i = 0; i < 16; i++)
        {
            var column = i % 4;
            if (column <= 1)
                leftPositions.Add(i); // 컬럼 0, 1: 왼쪽 영역
            else
                rightPositions.Add(i); // 컬럼 2, 3: 오른쪽 영역
        }
        ShuffleList(leftPositions);
        ShuffleList(rightPositions);

        var col = 0;
        var row = 0;
        List<int> position = new();
        for (var i = 0; i < 16; i++)
        {
            position.Add(i % 4 <= 1 ? leftPositions[col++] : rightPositions[row++]);
        }
        return noteList.Select(note => note.Random(position)).ToList();
    }

    private List<Note> FullRandom()
    {
        List<Note> result = new();
        foreach (var note in noteList)
        {
            List<(int, int)> validPositions = new();
            while (true)
            {
                var startTime = note.StartTime;
                var endTime = (note.IsLong ? note.FinishTime : startTime) + 31 / 30d;
                for (var r = 0; r < 4; r++)
                {
                    for (var c = 0; c < 4; c++)
                    {
                        if (IsAvailable(r, c, startTime, endTime))
                        {
                            validPositions.Add((r, c)); // 유효한 위치 추가
                        }
                    }
                }
                if (validPositions.Count < 1) // 불가능한 배치가 발생된 경우
                {
                    return FullRandom(); // 재시도
                }

                var chosenPosition = validPositions[UnityEngine.Random.Range(0, validPositions.Count)];
                var barRow = -1;
                var barColumn = -1;
                var row = chosenPosition.Item1;
                var column = chosenPosition.Item2;
                if (note.IsLong)
                {
                    // 롱노트의 경우 추가로 barRow, barColumn을 찾아야 함
                    validPositions = new();
                    for (var r = 0; r < 4; r++)
                    {
                        for (var c = 0; c < 4; c++)
                        {
                            var isRowValid = (row == r && column != c);
                            var isColValid = (row != r && column == c);

                            // Row == barRow 또는 Column == barColumn 중 하나만 만족해야 함
                            if ((isRowValid || isColValid) && IsAvailableArrow(r, c, startTime, endTime))
                            {
                                validPositions.Add((r, c)); // 유효한 위치 추가
                            }
                        }
                    }

                    if (validPositions.Count > 0) // 유효한 위치가 있는 경우
                    {
                        chosenPosition = validPositions[UnityEngine.Random.Range(0, validPositions.Count)];
                        barRow = chosenPosition.Item1;
                        barColumn = chosenPosition.Item2;
                    }
                    else
                    {
                        if (UnityEngine.Random.Range(0, 2) == 0) // 50% 확률로 Row와 일치할지, Column과 일치할지 결정
                        {
                            barRow = row;
                            barColumn = UnityEngine.Random.Range(0, 4);
                        }
                        else
                        {
                            barRow = column;
                            barColumn = UnityEngine.Random.Range(0, 4);
                        }
                    }
                    AddArrowTime(barRow, barColumn, startTime);
                }
                AddNoteTime(row, column, startTime, endTime);
                result.Add(note.Change(row, column, barRow, barColumn));
                break;
            }
        }
        return result;
    }

    public List<Note> Shuffle(GameMode mode)
    {
        return GameOptions.Instance.GameMode switch
        {
            GameMode.FullRandom => FullRandom(),
            GameMode.Random => Random(),
            GameMode.RandomPlus => RandomPlus(),
            GameMode.HalfRandom => HalfRandom(),
            _ => noteList
        };
    }

    private bool IsAvailable(int row, int column, double startTime, double endTime)
    {
        return !occupiedTimeGrid.TryGetValue((row, column), out var timeSlots) ||
               timeSlots.All(slot => endTime <= slot.Item1 || startTime >= slot.Item2);
    }

    private bool IsAvailableArrow(int row, int column, double startTime, double endTime)
    {
        return !occupiedTimeGridArrow.TryGetValue((row, column), out var timeSlots) ||
               timeSlots.All(slot => endTime <= slot.Item1 || startTime >= slot.Item2);
    }

    private void AddNoteTime(int row, int column, double startTime, double endTime)
    {
        occupiedTimeGrid.TryAdd((row, column), new());
        occupiedTimeGrid[(row, column)].Add((startTime, endTime));
    }

    private void AddArrowTime(int row, int column, double startTime)
    {
        occupiedTimeGridArrow.TryAdd((row, column), new());
        occupiedTimeGridArrow[(row, column)].Add((startTime, startTime + 23 / 30d));
    }
}

public class Note
{
    public int Row { get; private set; }
    public int Column { get; private set; }
    public int MusicBarIndex { get; set; }
    public Vector2 Position => MarkerManager.Instance.ConvertPosition(Row, Column);

    public bool IsLong => BarRow != -1 && BarColumn != -1;
    public int BarRow { get; private set; } = -1;
    public int BarColumn { get; private set; } = -1;
    public int MusicBarLongIndex { get; set; } = -1;
    public Vector2 BarPosition => MarkerManager.Instance.ConvertPosition(BarRow, BarColumn);

    public double StartTime { get; }
    public double FinishTime { get; set; } // 롱노트의 끝 판정

    public Note(int row, int column, double startTime)
    {
        Row = row;
        Column = column;
        StartTime = startTime;
    }

    public Note(int row, int column, int barRow, int barColumn, double startTime)
    {
        Row = row;
        Column = column;
        BarRow = barRow;
        BarColumn = barColumn;
        StartTime = startTime;
    }

    public Note Clone()
    {
        Note note = new(Row, Column, BarRow, BarColumn, StartTime);
        note.FinishTime = FinishTime;
        note.MusicBarIndex = MusicBarIndex;
        note.MusicBarLongIndex = MusicBarLongIndex;
        return note;
    }

    public Note Change(int row, int column, int barRow = -1, int barColumn = -1)
    {
        Note note = new(row, column, barRow, barColumn, StartTime);
        note.FinishTime = FinishTime;
        note.MusicBarIndex = MusicBarIndex;
        note.MusicBarLongIndex = MusicBarLongIndex;
        return note;
    }

    public Note Random(List<int> position)
    {
        var note = Clone();
        var newIndex = position[Row * 4 + Column];
        note.Row = newIndex / 4;
        note.Column = newIndex % 4;
        if (!IsLong) return note;

        newIndex = position[BarRow * 4 + BarColumn];
        note.BarRow = newIndex / 4;
        note.BarColumn = newIndex % 4;
        if (note.Row != note.BarRow && note.Column != note.BarColumn)
        {
            if (UnityEngine.Random.Range(0, 2) == 0)
            {
                note.BarRow = note.Row;
            }
            else
            {
                note.BarColumn = note.Column;
            }
        }
        note.FinishTime = FinishTime;
        return note;
    }

    public Note Random(List<int> row, List<int> column)
    {
        var note = Clone();
        note.Row = row[Row];
        note.Column = column[Column];
        if (!IsLong) return note;
        note.BarRow = row[BarRow];
        note.BarColumn = column[BarColumn];
        note.FinishTime = FinishTime;
        return note;
    }

    public Note Mirror()
    {
        var note = Clone();
        note.Column = 3 - note.Column;
        return note;
    }

    public Note Rotate(int degree)
    {
        var note = Clone();
        switch (degree % 360 / 90)
        {
            case 1: // 90도
                note.Row = 3 - Column;
                note.Column = Row;
                if (IsLong)
                {
                    note.BarRow = 3 - BarColumn;
                    note.BarColumn = BarRow;
                }
                break;
            case 2: // 180도
                note.Row = 3 - Row;
                note.Column = 3 - Column;
                if (IsLong)
                {
                    note.BarRow = 3 - BarRow;
                    note.BarColumn = 3 - BarColumn;
                }
                break;
            case 3: // 270도
                note.Row = Column;
                note.Column = 3 - Row;
                if (IsLong)
                {
                    note.BarRow = BarColumn;
                    note.BarColumn = 3 - BarRow;
                }
                break;
        }
        return note;
    }
}

public class ChartPart
{
    public double StartOffset { get; private set; }
    public List<string> NoteTimings { get; } = new();
    public List<List<string>> NotePositions { get; } = new() { new() };

    private readonly Chart chart;
    private readonly Dictionary<int, List<Note>> noteMap = new();

    private static int ConvertTimingChar(char timingChar)
    {
        if (timingChar is >= '０' and <= '９')
        {
            return timingChar - '０';
        }
        else if (timingChar is >= '①' and <= '⑳')
        {
            return timingChar - '①';
        }
        else if (timingChar is >= 'Ａ' and <= 'Ｚ')
        {
            return timingChar - 'Ａ' + 20;
        }
        return timingChar;
    }

    public ChartPart(double startOffset, Chart chart)
    {
        this.chart = chart;
        StartOffset = startOffset;
    }

    public void AddNotePositionText(string text)
    {
        var list = NotePositions[^1];
        if (list.Count == 4)
        {
            NotePositions.Add(list = new());
        }
        list.Add(text);
    }

    private void AddNote(Note newNote)
    {
        var index = newNote.Column + newNote.Row * 4;
        if (!noteMap.ContainsKey(index))
        {
            noteMap[index] = new();
        }

        var newIndex = noteMap[index].FindIndex(note => note.StartTime > newNote.StartTime);
        if (newIndex == -1)
        {
            noteMap[index].Add(newNote);
        }
        else
        {
            noteMap[index].Insert(newIndex, newNote);
        }
    }

    public void Convert()
    {
        Dictionary<int, double> timingMap = new();
        var currentBpm = chart.BpmList[^1];
        foreach (var timings in NoteTimings)
        {
            var length = Math.Max(4, timings.Length);
            var denominator = currentBpm * length;
            for (var index = 0; index < timings.Length; ++index)
            {
                var timingChar = timings[index];
                timingMap[ConvertTimingChar(timingChar)] = StartOffset + index * 60 / denominator;
            }
            StartOffset += timings.Length * 60 / denominator;
        }
        foreach (var noteGrid in NotePositions)
        {
            List<int> longNoteList = new();
            for (var yIndex = 0; yIndex < 4; ++yIndex)
            {
                for (var xIndex = 0; xIndex < noteGrid[yIndex].Length; ++xIndex)
                {
                    var note = noteGrid[yIndex][xIndex];
                    if (timingMap.ContainsKey(ConvertTimingChar(note))) continue; // A ~ Z 에 의해 타이밍 값인지 먼저 판단
                    switch (note)
                    {
                        case '^':
                        case '∧':
                        {
                            for (var newY = yIndex - 1; newY >= 0; --newY)
                            {
                                var index = newY * 4 + xIndex;
                                var longNoteChar = ConvertTimingChar(noteGrid[newY][xIndex]);
                                if (longNoteList.Contains(index) || !timingMap.TryGetValue(longNoteChar, out var value))
                                    continue;
                                longNoteList.Add(newY * 4 + xIndex);
                                AddNote(new(newY, xIndex, yIndex, xIndex, value));
                                break;
                            }
                            break;
                        }
                        case 'V':
                        case '∨':
                        case 'Ｖ':
                        {
                            for (var newY = yIndex + 1; newY < 4; ++newY)
                            {
                                var index = newY * 4 + xIndex;
                                var longNoteChar = ConvertTimingChar(noteGrid[newY][xIndex]);
                                if (longNoteList.Contains(index) || !timingMap.TryGetValue(longNoteChar, out var value))
                                    continue;
                                longNoteList.Add(newY * 4 + xIndex);
                                AddNote(new(newY, xIndex, yIndex, xIndex, value));
                                break;
                            }
                            break;
                        }
                        case '>':
                        case '＞':
                        {
                            for (var newX = xIndex + 1; newX < noteGrid[yIndex].Length; ++newX)
                            {
                                var index = yIndex * 4 + newX;
                                var longNoteChar = ConvertTimingChar(noteGrid[yIndex][newX]);
                                if (longNoteList.Contains(index) || !timingMap.TryGetValue(longNoteChar, out var value))
                                    continue;
                                longNoteList.Add(yIndex * 4 + newX);
                                AddNote(new(yIndex, newX, yIndex, xIndex, value));
                                break;
                            }
                            break;
                        }
                        case '＜':
                        case '<':
                        {
                            for (var newX = xIndex - 1; newX >= 0; --newX)
                            {
                                var index = yIndex * 4 + newX;
                                var longNoteChar = ConvertTimingChar(noteGrid[yIndex][newX]);
                                if (longNoteList.Contains(index) || !timingMap.TryGetValue(longNoteChar, out var value))
                                    continue;
                                longNoteList.Add(yIndex * 4 + newX);
                                AddNote(new(yIndex, newX, yIndex, xIndex, value));
                                break;
                            }
                            break;
                        }
                    }
                }
            }

            for (var yIndex = 0; yIndex < 4; ++yIndex)
            {
                for (var xIndex = 0; xIndex < noteGrid[yIndex].Length; ++xIndex)
                {
                    if (
                        !longNoteList.Contains(yIndex * 4 + xIndex) &&
                        timingMap.TryGetValue(ConvertTimingChar(noteGrid[yIndex][xIndex]), out var value)
                    )
                    {
                        AddNote(new(yIndex, xIndex, value));
                    }
                }
            }
        }
        foreach (var note in noteMap.SelectMany(item => item.Value))
        {
            chart.AddNote(note);
        }
    }
}
