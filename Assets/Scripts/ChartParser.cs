using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public class ChartParser : MonoBehaviour
{
}

public class Chart
{
    private static readonly Regex NOTE_REGEX = new(@"^([口□①-⑳┼｜┃━―∨∧^>＞＜<ＶＡ-Ｚ]{4}|([口□①-⑳┼｜┃━―∨∧^>＞＜<ＶＡ-Ｚ]{4}\|.+(\|)?))$", RegexOptions.Compiled);

    public string Name { get; private set; }
    public string Difficulty { get; private set; }
    public string FilePath { get; private set; }
    public bool IsLong { get; private set; } = false;

    /**
    * 음악이 시작되는 시간 
    * 값이 작아지면: 노래가 빨리재생됨(노래가 느릴때 이쪽으로)
    * 값이 커지면: 노래가 늦게재생됨(노래가 빠를때 이쪽으로)
    */
    public float StartOffset
    {
        get => GameManager.Instance.GetMusicOffset(Name);
    }

    public AudioClip bgmClip;

    public int NoteCount { get; private set; } = 0;

    /** 모든 박자가 들어가는 배열 */
    public readonly SortedSet<double> clapTimings = new();

    /** 곡의 BPM 목록 */
    public readonly List<double> bpmList = new();
    /** BPM이 변경되는 마디 목록 [변경되는마디] = 기존BPM 형태로 저장 */
    public readonly Dictionary<int, double> changeBpmMeasureList = new();

    /** 모든 노트가 들어가는 배열 [row * 4 + column] = List<Note> 형태 */
    private readonly Dictionary<int, List<Note>> gridNoteList = new();

    private List<Note> _allNotes = null;
    public List<Note> AllNotes
    {
        get
        {
            _allNotes ??= gridNoteList.SelectMany(pair => pair.Value).OrderBy(note => note.StartTime).ToList();
            return _allNotes;
        }
    }

    private Chart(string name, string difficulty, string filePath)
    {
        Name = name;
        FilePath = filePath;
        Difficulty = difficulty;
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

    public static Chart Parse(string musicName, string difficulty, string filePath)
    {
        string[] lines = File.ReadAllLines(filePath);
        Chart chart = new(musicName, difficulty, filePath);
        int beatIndex = 1;
        int measureIndex = 1;
        for (int i = 1; i < lines.Length; ++i)
        {
            var line = RemoveComment(lines[i]);
            if (line.Length < 1)
            {
                continue;
            }

            if ((line.StartsWith("bpm:", StringComparison.InvariantCultureIgnoreCase) || line.StartsWith("t=")) && TryParseDoubleInText(line, out double bpmValue))
            {
                if (chart.bpmList.Count > 0 && Mathf.Abs((float)(chart.bpmList[^1] - bpmValue)) <= float.Epsilon)
                {
                    continue;
                }
                if (chart.bpmList.Count > 0)
                {
                    //Debug.Log("[BPM 변경] 기존: " + chart.bpmList[^1] + ", 변경: " + bpmValue + ", bpm이 변경되는 마디: " + measureIndex);
                    chart.changeBpmMeasureList.Add(beatIndex, chart.bpmList[^1]);
                }
                chart.bpmList.Add(bpmValue);
                //Debug.Log("BPM SETTING: " + bpmValue);
            }
            else if (int.TryParse(line, out int _))
            {
                try
                {
                    int j = 0;
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
            /*if (!lastNote.IsLong && newNote.StartTime - lastNote.StartTime < (23 + 16) / 30f)
            {
                Debug.Log($"[{Name}] 충돌날 수 있는 노트 발견됨. 마디: {newNote.MeasureIndex}, Row: {newNote.Row}, Col: {newNote.Column}");
            }*/

            if (lastNote.IsLong && double.IsNaN(lastNote.FinishTime))
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

    public bool IsLong
    {
        get => BarRow != -1 && BarColumn != -1;
    }
    public int BarRow { get; private set; } = -1;
    public int BarColumn { get; private set; } = -1;
    public Vector2 BarPosition
    {
        get => MarkerManager.Instance.ConvertPosition(BarRow, BarColumn);
    }

    public double StartTime { get; private set; }
    public double FinishTime { get; set; } = double.NaN; // 롱노트의 끝 판정
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

        int index = noteMap[index].FindIndex(note => note.StartTime > newNote.StartTime);
        if (index == -1)
        {
            noteMap[index].Add(newNote);
        }
        else
        {
            noteMap[index].Insert(index, newNote);
        }
    }

    public void Convert()
    {
        Dictionary<int, double> timingMap = new();
        //Debug.Log("------------- 박자 시작 -------------");
        var currentBpm = chart.bpmList[^1];
        for (int yIndex = 0; yIndex < noteTimingStringList.Count; ++yIndex) // 한 구간을 4분음표로 취급하며 보편적으로 한마디에 4개의 박자가 있음
        {
            var timings = noteTimingStringList[yIndex].ToCharArray();
            for (int xIndex = 0; xIndex < timings.Length; ++xIndex)
            {
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
                            var longNoteChar = noteGrid[newY][xIndex];
                            if (timingMap.ContainsKey(longNoteChar))
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
                            var longNoteChar = noteGrid[newY][xIndex];
                            if (timingMap.ContainsKey(longNoteChar))
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
                            var longNoteChar = noteGrid[yIndex][newX];
                            if (timingMap.ContainsKey(longNoteChar))
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
                            var longNoteChar = noteGrid[yIndex][newX];
                            if (timingMap.ContainsKey(longNoteChar))
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
