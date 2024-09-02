using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; } = null;

    public Sprite defaultJacket;

    public readonly List<Music> musicList = new();

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
            case GameManager.SCENE_MUSIC_SELECT:
                UIManager.Instance.ResetMusicList();
                for (int i = 0; i < musicList.Count; ++i)
                {
                    UIManager.Instance.AddMusicButton(musicList[i]);
                }
                break;
        }
    }

    public void LoadMusicDir()
    {
        // TODO: info.json의 offset값으로 싱크 조절
        var basePath = Path.Combine(Application.dataPath, "..", "Songs");
        var syncPath = Path.Combine(basePath, "sync.txt");
        var syncList = GameManager.Instance.musicOffsetList;
        if (File.Exists(syncPath))
        {
            string[] lines = File.ReadAllLines(syncPath);
            foreach (string line in lines)
            {
                var split = line.Trim().Split(":");
                if (split.Length < 2)
                {
                    continue;
                }
                if (float.TryParse(split[1].Trim(), out float value))
                {
                    syncList[split[0].Trim()] = value;
                }
            }
        }

        UIManager.Instance.ResetMusicList();
        foreach (var dirPath in Directory.GetDirectories(basePath))
        {
            StartCoroutine(LoadMusic(dirPath));
        }
    }

    private IEnumerator LoadMusic(string dirPath)
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

        Sprite sprite = defaultJacket;
        string[] jacketFiles = Directory.GetFiles(dirPath, $"jacket.*");
        string[] extensions = new[] { "png", "jpg", "jpeg", "bmp" };
        if (jacketFiles.Length > 0)
        {
            byte[] fileData = File.ReadAllBytes(jacketFiles[0]);
            Texture2D texture = new(2, 2);
            if (texture.LoadImage(fileData))
            {
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
        }

        string jsonPath = Path.Combine(dirPath, "info.json");
        if (File.Exists(jsonPath))
        {
            try
            {
                string json = File.ReadAllText(jsonPath);
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
        if (music.AvailableDifficulty.Count > 0)
        {
            musicList.Add(music);
            UIManager.Instance.AddMusicButton(music);
        }
    }
}

public class Music{
    public readonly string title;
    public readonly string author;

    public readonly string path;
    public readonly AudioClip clip;
    public readonly Sprite jacket = null;
    public readonly Dictionary<Difficulty, int> scoreList = new();

    /**
    * 음악이 시작되는 시간 
    * 값이 작아지면: 노래가 빨리재생됨(노래가 느릴때 이쪽으로)
    * 값이 커지면: 노래가 늦게재생됨(노래가 빠를때 이쪽으로)
    */
    public float StartOffset
    {
        get => GameManager.Instance.GetMusicOffset(title);
    }

    public List<Difficulty> AvailableDifficulty
    {
        get => chartList.Keys.ToList();
    }

    public bool IsLong { get; private set; } = false;

    private readonly Dictionary<Difficulty, Chart> chartList = new();

    public Music(AudioClip clip, string path, string title, string author, Sprite jacket = null)
    {
        this.clip = clip;
        this.path = path;
        this.title = title;
        this.author = author;
        this.jacket = jacket;
        title = Path.GetFileName(path);
    }

    public void AddChart(Chart chart)
    {
        if (!chartList.ContainsKey(chart.difficulty))
        {
            chartList[chart.difficulty] = chart;
            IsLong = IsLong || chart.IsLong;
        }
    }

    public Chart GetChart(Difficulty difficulty)
    {
        if (!chartList.ContainsKey(difficulty))
        {
            return null;
        }
        return chartList[difficulty];
    }

    public bool CanPlay(Difficulty difficulty)
    {
        return chartList.ContainsKey(difficulty);
    }

    public int GetScore(Difficulty difficulty)
    {
        return scoreList.ContainsKey(difficulty) ? scoreList[difficulty] : 0;
    }

    public void SetScore(Difficulty difficulty, int score)
    {
        scoreList[difficulty] = score;
    }
}

public class Chart
{
    public static readonly Regex NOTE_REGEX = new(@"^([口□①-⑳┼｜┃━―∨∧^>＞＜<ＶＡ-Ｚ]{4}|([口□①-⑳┼｜┃━―∨∧^>＞＜<ＶＡ-Ｚ]{4}\|.+(\|)?))$", RegexOptions.Compiled);

    public readonly Music music;
    public readonly double level;
    public readonly Difficulty difficulty;

    /** 곡의 BPM 목록 */
    public readonly List<double> bpmList = new();
    /** 모든 박자가 들어가는 배열 */
    public readonly SortedSet<double> clapTimings = new();
    /** BPM이 변경되는 마디 목록 [변경되는마디] = 기존BPM 형태로 저장 */
    public readonly Dictionary<int, double> changeBpmMeasureList = new();

    public int NoteCount { get; private set; } = 0;
    public bool IsLong { get; private set; } = false;
    public List<Note> NoteList
    {
        get
        {
            _allNotes ??= gridNoteList.SelectMany(pair => pair.Value).OrderBy(note => note.StartTime).ToList();
            return _allNotes;
        }
    }

    /** 모든 노트의 출현 순서별 정렬 */
    private List<Note> _allNotes = null;
    /** 그리드 별 노트 배열 [row * 4 + column] = List<Note> */
    private readonly Dictionary<int, List<Note>> gridNoteList = new();

    public List<int> MusicBar
    {
        get
        {
            List<int> result = new(new int[120]);
            var offset = 29d / 60d - music.StartOffset;

            int noteIndex = 0;
            int noteCount = NoteList.Count;
            for (int musicIndex = 1, limit = result.Count; musicIndex <= limit; ++musicIndex)
            {
                var musicMin = music.clip.length * (musicIndex - 1) / limit;
                var musicMax = music.clip.length * musicIndex / limit;
                while (noteIndex < noteCount)
                {
                    var note = NoteList[noteIndex];
                    var time = note.StartTime - offset;

                    if (time >= musicMax)
                    {
                        break;
                    }

                    if (time >= musicMin)
                    {
                        ++result[musicIndex - 1];
                        var finishTime = note.FinishTime - offset;
                        if (musicMin <= finishTime && finishTime < musicMax)
                        {
                            ++result[musicIndex - 1];
                        }
                    }
                    noteIndex++;
                }
            }
            return result;
        }
    }

    public int Score
    {
        get => music.GetScore(difficulty);
    }

    private Chart(Music music, double level, Difficulty difficulty)
    {
        this.music = music;
        this.level = level;
        this.difficulty = difficulty;
    }

    private static bool TryParseDoubleInText(string text, out double result)
    {
        var match = Regex.Match(text, @"-?\d+(\.\d+)?");
        return double.TryParse(match.Success ? match.Value : "", out result);
    }

    private static string RemoveComment(string text)
    {
        var commentIndex = text.IndexOf("//");
        return (commentIndex > 0 ? text[..commentIndex].Trim() : text.Trim()).Replace(" ", "");
    }

    public static bool IsNoteText(string text)
    {
        return NOTE_REGEX.IsMatch(text);
    }

    public static Chart Parse(Music music, Difficulty difficulty)
    {
        var diffStr = difficulty.ToString().ToLower();
        var filePath = Path.Combine(music.path, $"{diffStr}.txt");
        if (!File.Exists(filePath))
        {
            //Debug.Log($"{musicName}의 {diffStr}채보가 발견되지 않았습니다.");
            return null;
        }

        double level = 1;
        string[] lines = File.ReadAllLines(filePath);
        foreach (var text in lines)
        {
            var line = RemoveComment(text).ToLower();
            if (line.StartsWith("lev") && TryParseDoubleInText(line, out level))
            {
                break;
            }
        }
        Chart chart = new(music, level, difficulty);
        int beatIndex = 1;
        int measureIndex = 1;
        for (int i = 0; i < lines.Length; ++i)
        {
            var line = RemoveComment(lines[i]);
            if (line.Length < 1)
            {
                continue;
            }

            var lineLower = line.ToLower();
            if ((lineLower.StartsWith("bpm:") || lineLower.StartsWith("t=")) && TryParseDoubleInText(line, out double bpmValue))
            {
                if (chart.bpmList.Count < 1 || Math.Abs(chart.bpmList[^1] - bpmValue) > 0.01)
                {
                    if (chart.bpmList.Count > 0)
                    {
                        //Debug.Log($"[BPM 변경] 기존: {chart.bpmList[^1]}, 변경: {bpmValue}, 변경 시작 마디: {measureIndex}, 비트: {beatIndex}");
                        chart.changeBpmMeasureList.Add(beatIndex - 1, chart.bpmList[^1]);
                    }
                    chart.bpmList.Add(bpmValue);
                    //Debug.Log("BPM SETTING: " + bpmValue);
                }
            }
            else if (IsNoteText(line))
            {
                try
                {
                    int j = -1;
                    var measure = new Measure(measureIndex++, beatIndex, chart);
                    //Debug.Log($"------------- 마디의 시작: {beatIndex} --------------------");
                    while (lines.Length > i + ++j)
                    {
                        string noteAndTiming = RemoveComment(lines[i + j]);
                        if (noteAndTiming.Length < 1)
                        {
                            continue;
                        }

                        //Debug.Log($"현재 라인: {i + j}, 값: {noteAndTiming}");
                        if (!IsNoteText(noteAndTiming))
                        {
                            --j;
                            break;
                        }

                        string gridPart = noteAndTiming[..4];
                        measure.AddNotePositionText(gridPart);

                        var timingSplit = noteAndTiming[4..].Trim().Split("|");
                        if (timingSplit.Length > 1)
                        {
                            var timingText = timingSplit[1].Trim();
                            if (measure.noteTimingStringList.Count > 0 && measure.noteTimingStringList[^1].Length < 4)
                            {
                                measure.noteTimingStringList[^1] += timingText;
                            }
                            else
                            {
                                measure.noteTimingStringList.Add(timingText);
                            }
                        }
                    }
                    //Debug.Log($"------------- 마디의 종료: {beatIndex} --------------------");
                    measure.Convert();
                    i += j;
                    beatIndex += measure.noteTimingStringList.Count;
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

        int index = newNote.Row * 4 + newNote.Column;
        if (!gridNoteList.ContainsKey(index))
        {
            gridNoteList.Add(index, new List<Note> { newNote });
        }
        else
        {
            var noteList = gridNoteList[index];
            var lastNote = noteList[^1];
            /*if (!lastNote.IsLong && newNote.StartTime - lastNote.StartTime < 23 / 30f)
            {
                Debug.Log($"[{Name}] 충돌날 수 있는 노트 발견됨. 마디: {newNote.MeasureIndex}, Row: {newNote.Row}, Col: {newNote.Column}");
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
        clapTimings.Add(newNote.StartTime);
    }
}

public class Note
{
    public int Row { get; private set; }
    public int Column { get; private set; }
    public Vector2 Position
    {
        get => MarkerManager.Instance.ConvertPosition(Row, Column);
    }

    public int BarRow { get; private set; } = -1;
    public int BarColumn { get; private set; } = -1;
    public Vector2 BarPosition
    {
        get => MarkerManager.Instance.ConvertPosition(BarRow, BarColumn);
    }

    public bool IsLong { get => BarRow != -1 && BarColumn != -1; }

    public double StartTime { get; private set; }
    public double FinishTime { get; set; } = 0; // 롱노트의 끝 판정
    public int MeasureIndex { get; private set; }

    public Note(int measureIndex, int row, int column, double startTime)
    {
        MeasureIndex = measureIndex;
        Row = row;
        Column = column;
        StartTime = startTime;
    }

    public Note(int measureIndex, int row, int column, int barRow, int barColumn, double startTime)
    {
        MeasureIndex = measureIndex;
        Row = row;
        Column = column;
        BarRow = barRow;
        BarColumn = barColumn;
        StartTime = startTime;
    }
}

public class Measure
{
    public List<string> noteTimingStringList = new();
    public List<List<string>> notePositionStringList = new() { new() };

    public int BeatIndex { get; private set; }
    public int MeasureIndex { get; private set; }

    private readonly Chart chart;
    private readonly Dictionary<int, List<Note>> noteMap = new();

    public Measure(int measureIndex, int beatIndex, Chart chart)
    {
        this.chart = chart;
        BeatIndex = beatIndex;
        MeasureIndex = measureIndex;
    }

    public double ConvertBeatToTime(int beatNumber)
    {
        var resultBeat = 0.0;
        int beforeBeatIndex = 1;
        var currentBpm = chart.bpmList[^1];
        foreach (var item in chart.changeBpmMeasureList)
        {
            resultBeat += (item.Key - beforeBeatIndex + 1) * 60 / item.Value; // 변속 전까지의 길이
            beforeBeatIndex = item.Key + 1;
        }
        resultBeat += (beatNumber - beforeBeatIndex) * 60 / currentBpm; // 현재 박자의 실제 시작 시간
        return resultBeat;
    }

    public void AddNotePositionText(string text)
    {
        List<string> list = notePositionStringList[^1];
        if (list.Count == 4)
        {
            notePositionStringList.Add(list = new());
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
        //Debug.Log("------------- 박자 시작 -------------");
        var currentBpm = chart.bpmList[^1];
        //Debug.Log($"currentBpm: {currentBpm}");
        for (int yIndex = 0; yIndex < noteTimingStringList.Count; ++yIndex) // 한 구간을 4분음표로 취급하며 보편적으로 한마디에 4개의 박자가 있음
        {
            var timings = noteTimingStringList[yIndex].ToCharArray();
            //Debug.Log($"Count: {timings.Length}");
            for (int xIndex = 0; xIndex < timings.Length; ++xIndex)
            {
                if (timings[xIndex] == '－')
                {
                    continue;
                }
                //int currentBeat = 60 / (currentBpm * timings.Length); // 현재 박자의 길이, 16분음표 등등
                timingMap[timings[xIndex]] = ConvertBeatToTime(BeatIndex + yIndex - 1) + xIndex * 60 / (currentBpm * timings.Length);
            }
        }
        //Debug.Log($"------------- 노트 시작: {MeasureIndex} -------------");
        foreach (var noteGrid in notePositionStringList)
        {
            List<int> longNoteList = new();
            for (int yIndex = 0; yIndex < 4; ++yIndex)
            {
                for (int xIndex = 0; xIndex < noteGrid[yIndex].Length; ++xIndex)
                {
                    var note = noteGrid[yIndex][xIndex];
                    if (note == '^' || note == '∧')
                    {
                        for (int newY = yIndex - 1; newY >= 0; --newY)
                        {
                            var index = newY * 4 + xIndex;
                            var longNoteChar = noteGrid[newY][xIndex];
                            if (!longNoteList.Contains(index) && timingMap.ContainsKey(longNoteChar))
                            {
                                longNoteList.Add(newY * 4 + xIndex);
                                AddNote(new(MeasureIndex, newY, xIndex, yIndex, xIndex, timingMap[longNoteChar]));
                                //Debug.Log("[롱노트 추가됨] 현재 xIndex: " + xIndex + ", note: " + note + ", longNoteChar: " + longNoteChar);
                                break;
                            }
                        }
                    }
                    else if (note == '∨' || note == 'Ｖ')
                    {
                        for (int newY = yIndex + 1; newY < 4; ++newY)
                        {
                            var index = newY * 4 + xIndex;
                            var longNoteChar = noteGrid[newY][xIndex];
                            if (!longNoteList.Contains(index) && timingMap.ContainsKey(longNoteChar))
                            {
                                longNoteList.Add(newY * 4 + xIndex);
                                AddNote(new(MeasureIndex, newY, xIndex, yIndex, xIndex, timingMap[longNoteChar]));
                                //Debug.Log("[롱노트 추가됨] 현재 xIndex: " + xIndex + ", note: " + note + ", longNoteChar: " + longNoteChar);
                                break;
                            }
                        }
                    }
                    else if (note == '>' || note == '＞')
                    {
                        for (int newX = xIndex + 1; newX < noteGrid[yIndex].Length; ++newX)
                        {
                            var index = yIndex * 4 + newX;
                            var longNoteChar = noteGrid[yIndex][newX];
                            if (!longNoteList.Contains(index) && timingMap.ContainsKey(longNoteChar))
                            {
                                longNoteList.Add(yIndex * 4 + newX);
                                AddNote(new(MeasureIndex, yIndex, newX, yIndex, xIndex, timingMap[longNoteChar]));
                                //Debug.Log("[롱노트 추가됨] 현재 xIndex: " + xIndex + ", note: " + note + ", longNoteChar: " + longNoteChar);
                                break;
                            }
                        }
                    }
                    else if (note == '＜' || note == '<')
                    {
                        for (int newX = xIndex - 1; newX >= 0; --newX)
                        {
                            var index = yIndex * 4 + newX;
                            var longNoteChar = noteGrid[yIndex][newX];
                            if (!longNoteList.Contains(index) && timingMap.ContainsKey(longNoteChar))
                            {
                                longNoteList.Add(yIndex * 4 + newX);
                                AddNote(new(MeasureIndex, yIndex, newX, yIndex, xIndex, timingMap[longNoteChar]));
                                //Debug.Log("[롱노트 추가됨] 현재 xIndex: " + xIndex + ", note: " + note + ", longNoteChar: " + longNoteChar);
                                break;
                            }
                        }
                    }
                }
            }

            for (int yIndex = 0; yIndex < 4; ++yIndex)
            {
                for (int xIndex = 0; xIndex < noteGrid[yIndex].Length; ++xIndex)
                {
                    var noteChar = noteGrid[yIndex][xIndex];
                    if (!longNoteList.Contains(yIndex * 4 + xIndex) && timingMap.ContainsKey(noteChar))
                    {
                        AddNote(new(MeasureIndex, yIndex, xIndex, timingMap[noteChar]));
                        //Debug.Log($"[노트 추가됨] 현재 xIndex: {xIndex}, yIndex: {yIndex}, note: {noteChar}");
                    }
                }
            }
        }
        foreach (var item in noteMap)
        {
            foreach (var note in item.Value)
            {
                //Debug.Log($"{item.Key}: {note.StartTime}");
                chart.AddNote(note);
            }
            //Debug.Log($"-------------------------");
        }
        //Debug.Log($"------------- 노트 종료: {MeasureIndex} -------------");
    }
}
