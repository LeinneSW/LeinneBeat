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
    public string author;
    public float offset;
    public float preview;
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

    public readonly List<Music> MusicList = new();
    public Dictionary<string, float> MusicOffsetList { get; } = new();

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

    public void LoadMusicDir()
    {
        // TODO: info.json의 offset값으로 싱크 조절
        var basePath = Path.Combine(Application.dataPath, "..", "Songs");
        var syncPath = Path.Combine(basePath, "sync.txt");
        if (File.Exists(syncPath))
        {
            var lines = File.ReadAllLines(syncPath);
            foreach (var line in lines)
            {
                var split = line.Trim().Split(":");
                if (split.Length < 2)
                {
                    continue;
                }
                if (float.TryParse(split[1].Trim(), out var value))
                {
                    MusicOffsetList[split[0].Trim()] = value;
                }
            }
        }

        Dictionary<string, Dictionary<string, MusicScoreData>> scoreDataList;
        var scorePath = Path.Combine(basePath, "score.json");
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
        var totalCount = allFiles.Length;
        UIManager.Instance.ResetMusicList();
        foreach (var dirPath in allFiles)
        {
            StartCoroutine(LoadMusic(dirPath, success =>
            {
                if (!success) --totalCount;
                if (totalCount > MusicList.Count) return;

                UIManager.Instance.SortMusicByName();
                foreach (var (musicName, difficultyTable) in scoreDataList)
                {
                    var music = MusicList.Find(music => music.Title == musicName);
                    if (music == null) continue;
                    foreach (var (difficultyStr, scoreData) in difficultyTable)
                    {
                        if (!Enum.TryParse(difficultyStr, true, out Difficulty difficulty)) continue;
                        music.SetScore(difficulty, scoreData.score);
                        music.SetMusicBarScore(difficulty, scoreData.musicBar);
                    }
                }
            }));
        }
    }

    private IEnumerator LoadMusic(string dirPath, Action<bool> afterFunction)
    {
        var title = Path.GetFileName(dirPath);
        var author = "작곡가";
        var songFiles = Directory.GetFiles(dirPath, "song.*");
        if (songFiles.Length < 1)
        {
            Debug.Log($"'{title}' 폴더엔 음악 파일이 존재하지 않습니다.");
            yield break;
        }

        var musicPath = songFiles[0];
        var audioType = GetAudioType(Path.GetExtension(musicPath));
        if (audioType == AudioType.UNKNOWN)
        {
            Debug.Log($"'{title}' 폴더엔 음악 파일이 존재하지 않습니다.");
            yield break;
        }

        using var www = UnityWebRequestMultimedia.GetAudioClip("file://" + musicPath, audioType);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log($"폴더: {musicPath}, 오류: {www.error}");
            yield break;
        }

        var sprite = DefaultJacket;
        var jacketFiles = Directory.GetFiles(dirPath, $"jacket.*");
        if (jacketFiles.Length > 0)
        {
            var fileData = File.ReadAllBytes(jacketFiles[0]);
            Texture2D texture = new(2, 2);
            if (texture.LoadImage(fileData))
            {
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
        }

        var jsonPath = Path.Combine(dirPath, "info.json");
        if (File.Exists(jsonPath))
        {
            try
            {
                var json = File.ReadAllText(jsonPath);
                var jsonData = JsonUtility.FromJson<MusicInfo>(json);
                title = jsonData.title;
                author = jsonData.author ?? author;
            }
            catch
            {
                Debug.Log($"곡 이름: {title}");
            }
        }

        Music music = new(DownloadHandlerAudioClip.GetContent(www), dirPath, title, author, sprite);
        _ = music.StartOffset; // TODO: remove HACK
        foreach (var difficulty in Enum.GetValues(typeof(Difficulty)))
        {
            var chart = Chart.Parse(music, (Difficulty) difficulty);
            if (chart != null)
            {
                music.AddChart(chart);
            }
        }

        if (music.IsValid)
        {
            MusicList.Add(music);
        }
        afterFunction(music.IsValid);
    }

    public void SetMusicOffset(string title, float offset)
    {
        MusicOffsetList[title] = offset;
    }

    public float GetMusicOffset(string title)
    {
        MusicOffsetList.TryAdd(title, 0);
        return MusicOffsetList[title];
    }

    public async Task SaveMusicScore(Difficulty difficulty, Music music)
    {
        /*Dictionary<string, Dictionary<string, MusicScoreData>> json = new();
        foreach (var m in MusicList)
        {
            json[m.Title] = new();
            for (var i = 0; i < 3; ++i)
            {
                var diff = (Difficulty)i;
                if (m.CanPlay(diff))
                {
                    json[m.Title][diff.ToString()] = new()
                    {
                        score = m.GetScore(diff),
                        musicBar = m.GetMusicBarScore(diff)
                    };
                }
            }
        }*/
        Dictionary<string, Dictionary<string, MusicScoreData>> json;
        var scorePath = Path.Combine(Application.dataPath, "..", "Songs", "score.json");
        if (File.Exists(scorePath))
        {
            var text = await File.ReadAllTextAsync(scorePath);
            json = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, MusicScoreData>>>(text);
        }
        else
        {
            json = new();
        }

        json.TryAdd(music.Title, new());
        json[music.Title][difficulty.ToString()] = new()
        {
            score = music.GetScore(difficulty),
            musicBar = music.GetMusicBarScore(difficulty)
        };

        var jsonStr = JsonConvert.SerializeObject(json);
        await File.WriteAllTextAsync(scorePath, jsonStr);
    }

    public async Task SaveMusicOffset(string name)
    {
        var startOffset = GetMusicOffset(name);
        var path = Path.Combine(Application.dataPath, "..", "Songs", "sync.txt");
        List<string> lines;
        if (File.Exists(path))
        {
            lines = new(await File.ReadAllLinesAsync(path));
        }
        else
        {
            lines = new();
        }

        var find = false;
        for (var i = lines.Count - 1; i >= 0; --i)
        {
            var line = lines[i];
            if (!lines[i].StartsWith($"{name}:")) continue;
            lines[i] = $"{name}:{startOffset}";
            if (line == lines[i]) return; // 동일할경우 저장하지 않음
            find = true;
            break;
        }
        if (!find)
        {
            lines.Add($"{name}:{startOffset}");
        }
        await File.WriteAllLinesAsync(path, lines);
    }
}

public class Music{
    public readonly string Title;
    public readonly string Author;
    public readonly float Preview = 35f;

    public readonly string Path;
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

    /**
    * 음악이 시작되는 시간 
    * 값이 작아지면: 노래가 빨리재생됨(노래가 느릴때 이쪽으로)
    * 값이 커지면: 노래가 늦게재생됨(노래가 빠를때 이쪽으로)
    */
    public float StartOffset
    {
        get => MusicManager.Instance.GetMusicOffset(Title);
        set => MusicManager.Instance.SetMusicOffset(Title, value);
    }

    public bool IsValid => chartList.Count > 0;

    public bool IsLong { get; private set; }

    private readonly Dictionary<Difficulty, Chart> chartList = new();

    public Music(AudioClip clip, string path, string title, string author, Sprite jacket = null)
    {
        Clip = clip;
        Path = path;
        Title = title;
        Author = author;
        Jacket = jacket;
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
        if (!GameOptions.Instance.AutoPlay && ScoreList[difficulty] < score)
        {
            ScoreList[difficulty] = score;
        }
    }

    public void SetMusicBarScore(Difficulty difficulty, List<int> score)
    {
        if (GameOptions.Instance.AutoPlay)
        {
            return;
        }
        for (int i = 0, length = Math.Min(score.Count, 120); i < length; ++i)
        {
            MusicBarScoreList[difficulty][i] = Math.Max(MusicBarScoreList[difficulty][i], score[i]);
        }
    }
}

public class Chart
{
    public static readonly Regex NoteRegex = new(@"^([口□①-⑳┼｜┃━―∨∧^>＞＜<ＶＡ-Ｚ]{4}|([口□①-⑳┼｜┃━―∨∧^>＞＜<ＶＡ-Ｚ]{4}\|.+(\|)?))$", RegexOptions.Compiled);

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
    public List<int> MusicBar
    {
        get
        {
            if (musicBar != null)
            {
                return musicBar;
            }
            musicBar = new(new int[120]);
            var limit = musicBar.Count;
            var divide = Music.Clip.length / limit;
            var offset = 29d / 60d - Music.StartOffset;
            foreach (var note in NoteList)
            {
                var startBarIndex = (int) Math.Floor((note.StartTime - offset) / divide);
                musicBar[startBarIndex]++;
                note.MusicBarIndex = startBarIndex;

                if (!(note.FinishTime > 0)) continue;
                var finishBarIndex = (int)Math.Floor((note.FinishTime - offset) / divide);
                musicBar[finishBarIndex]++;
                note.MusicBarLongIndex = finishBarIndex;
            }
            return musicBar;
        }
    }

    public int Score => Music.GetScore(Difficulty);
    public List<int> MusicBarScore => Music.GetMusicBarScore(Difficulty);

    private Chart(Music music, double level, Difficulty difficulty)
    {
        Music = music;
        Level = level;
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
        return NoteRegex.IsMatch(text);
    }

    public static bool IsBpmText(string text)
    {
        var lower = text.ToLower();
        return lower.StartsWith("bpm") || lower.StartsWith("t=");
    }

    public static Chart Parse(Music music, Difficulty difficulty)
    {
        var diffStr = difficulty.ToString().ToLower();
        var filePath = Path.Combine(music.Path, $"{diffStr}.txt");
        if (!File.Exists(filePath))
        {
            return null;
        }

        var level = 1.0;
        var lines = File.ReadAllLines(filePath);
        foreach (var text in lines)
        {
            var line = RemoveComment(text).ToLower();
            if (line.StartsWith("lev") && TryParseDoubleInText(line, out level))
            {
                break;
            }
        }
        Chart chart = new(music, level, difficulty);
        var startOffset = 0.0;
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
                try
                {
                    var j = -1;
                    var chartPart = new ChartPart(startOffset, chart);
                    while (lines.Length > i + ++j)
                    {
                        var noteAndTiming = RemoveComment(lines[i + j]);
                        if (noteAndTiming.Length < 1)
                        {
                            continue;
                        }

                        if (!IsNoteText(noteAndTiming))
                        {
                            --j;
                            break;
                        }

                        var gridPart = noteAndTiming[..4];
                        chartPart.AddNotePositionText(gridPart);

                        var timingSplit = noteAndTiming[4..].Trim().Split("|");
                        if (timingSplit.Length <= 1) continue;
                        chartPart.NoteTimings.Add(timingSplit[1].Trim());
                    }
                    chartPart.Convert();
                    i += j;
                    startOffset = chartPart.StartOffset;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    return null;
                }
            }
        }
        return chart;
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
            /*if (!lastNote.IsLong && newNote.StartTime - lastNote.StartTime < 23 / 30f)
            {
                Debug.Log($"[{Name}] 충돌날 수 있는 노트 발견됨. 시작시간: {newNote.StartTime}, Row: {newNote.Row}, Col: {newNote.Column}");
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
    public double FinishTime { get; set; } = 0; // 롱노트의 끝 판정

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
            foreach (var timingChar in timings)
            {
                //int currentBeat = 60 / (currentBpm * timings.Length); // 현재 박자의 길이, 16분음표 등등
                timingMap[timingChar] = StartOffset;
                StartOffset += 60 / (currentBpm * length);
            }
        }
        foreach (var noteGrid in NotePositions)
        {
            List<int> longNoteList = new();
            for (var yIndex = 0; yIndex < 4; ++yIndex)
            {
                for (var xIndex = 0; xIndex < noteGrid[yIndex].Length; ++xIndex)
                {
                    var note = noteGrid[yIndex][xIndex];
                    switch (note)
                    {
                        case '^':
                        case '∧':
                        {
                            for (var newY = yIndex - 1; newY >= 0; --newY)
                            {
                                var index = newY * 4 + xIndex;
                                var longNoteChar = noteGrid[newY][xIndex];
                                if (longNoteList.Contains(index) || !timingMap.TryGetValue(longNoteChar, out var value))
                                    continue;
                                longNoteList.Add(newY * 4 + xIndex);
                                AddNote(new(newY, xIndex, yIndex, xIndex, value));
                                break;
                            }
                            break;
                        }
                        case '∨':
                        case 'Ｖ':
                        {
                            for (var newY = yIndex + 1; newY < 4; ++newY)
                            {
                                var index = newY * 4 + xIndex;
                                var longNoteChar = noteGrid[newY][xIndex];
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
                                var longNoteChar = noteGrid[yIndex][newX];
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
                                var longNoteChar = noteGrid[yIndex][newX];
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
                    var noteChar = noteGrid[yIndex][xIndex];
                    if (!longNoteList.Contains(yIndex * 4 + xIndex) && timingMap.TryGetValue(noteChar, out var value))
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
