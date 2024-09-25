using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

[JsonConverter(typeof(SymbolicTimeConverter))]
public class SymbolicTime
{
    public double BeatValue { get; set; }

    public SymbolicTime(double value)
    {
        BeatValue = value;
    }
}

public class SymbolicTimeConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(SymbolicTime);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        var token = JToken.Load(reader);

        if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
        {
            double value = token.ToObject<double>();
            return new SymbolicTime(value);
        }
        else if (token.Type == JTokenType.Array)
        {
            var array = token.ToObject<int[]>();
            if (array.Length == 3 && array[2] != 0)
            {
                double value = array[0] + (double)array[1] / array[2];
                return new SymbolicTime(value);
            }
            else
            {
                throw new JsonSerializationException("Invalid symbolic time array.");
            }
        }
        else if (token.Type == JTokenType.String)
        {
            if (double.TryParse(token.ToString(), out double value))
            {
                return new SymbolicTime(value);
            }
            else
            {
                throw new JsonSerializationException("Invalid symbolic time string.");
            }
        }
        else
        {
            throw new JsonSerializationException("Invalid symbolic time format.");
        }
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        var symbolicTime = (SymbolicTime)value;
        writer.WriteValue(symbolicTime.BeatValue);
    }
}

public class BpmEvent
{
    [JsonProperty("beat")]
    public int Tick { get; set; } // 틱 단위의 BPM 변경 시점

    [JsonProperty("bpm")]
    public double Bpm { get; set; }
}

public class Timing
{
    [JsonProperty("offset")]
    public double Offset { get; set; } = 0;

    [JsonProperty("resolution")]
    public int Resolution { get; set; } = 240;

    private List<BpmEvent> bpms;

    [JsonProperty("bpms")]
    public List<BpmEvent> Bpms
    {
        get => bpms;
        set
        {
            if (value == null || value.Count == 0)
            {
                throw new InvalidDataException("Bpms list cannot be null or empty.");
            }

            // bpmList를 Tick 기준으로 정렬
            bpms = value.OrderBy(b => b.Tick).ToList();
        }
    }

    [JsonProperty("hakus")]
    public List<object> Hakus { get; set; } = new List<object>(); // 필요에 따라 구현
}

public class Metadata
{
    [JsonProperty("title")]
    public string Title { get; set; }

    [JsonProperty("artist")]
    public string Artist { get; set; }

    [JsonProperty("audio")]
    public string Audio { get; set; }

    [JsonProperty("jacket")]
    public string Jacket { get; set; }

    [JsonProperty("preview")]
    public Preview Preview { get; set; }
}

public class Preview
{
    [JsonProperty("start")]
    public double Start { get; set; }

    [JsonProperty("duration")]
    public double Duration { get; set; }
}

public class RawNote
{
    [JsonProperty("n")]
    public int N { get; set; } // 노트의 위치 (0~15)

    [JsonProperty("t")]
    public int T { get; set; } // 시작 시간 (tick)

    [JsonProperty("l")]
    public int? L { get; set; } // 종료 시간 (tick, 롱노트인 경우)

    [JsonProperty("p")]
    public int P { get; set; } // 롱노트의 화살표 방향 (롱노트인 경우)
}

public class Note
{
    public int Row { get; set; }
    public int Column { get; set; }
    public double StartTime { get; set; }
    public double FinishTime { get; set; } // 롱노트가 아닐 경우 StartTime과 동일
    public int BarRow { get; set; }
    public int BarColumn { get; set; }
    public bool IsLongNote { get; set; }
}


public class Chart
{
    [JsonProperty("level")]
    public double Level { get; set; }

    [JsonProperty("resolution")]
    public int? Resolution { get; set; }

    [JsonProperty("timing")]
    public Timing Timing { get; set; }

    [JsonProperty("notes")]
    public List<RawNote> RawNotes { get; set; }

    [JsonIgnore]
    public List<Note> Notes { get; set; }
}

public class Music
{
    [JsonProperty("version")]
    public string Version { get; set; }

    [JsonProperty("metadata")]
    public Metadata Metadata { get; set; }

    [JsonProperty("timing")]
    public Timing Timing { get; set; }

    [JsonProperty("data")]
    public Dictionary<string, Chart> Charts { get; set; }

    public Chart GetChart(string difficulty)
    {
        if (Charts.ContainsKey(difficulty))
        {
            return Charts[difficulty];
        }
        else
        {
            return null;
        }
    }
}

public static class MusicParser
{
    public static Music LoadFromFile(string filePath)
    {
        try
        {
            string jsonContent = File.ReadAllText(filePath);
            Music music = JsonConvert.DeserializeObject<Music>(jsonContent);

            // Timing 객체와 Bpms 리스트의 유효성 검사
            if (music.Timing == null || music.Timing.Bpms == null || music.Timing.Bpms.Count == 0)
            {
                throw new InvalidDataException("Invalid chart file: Timing information is missing or incomplete.");
            }

            // 각 차트의 노트를 파싱하여 실제 Note 객체로 변환
            foreach (var chartEntry in music.Charts)
            {
                string difficulty = chartEntry.Key;
                Chart chart = chartEntry.Value;

                // 상위의 resolution과 timing을 상속받음
                if (!chart.Resolution.HasValue)
                    chart.Resolution = music.Timing.Resolution;

                if (chart.Timing == null)
                    chart.Timing = music.Timing;
                else
                {
                    // Chart의 Timing 객체와 Bpms 리스트의 유효성 검사
                    if (chart.Timing.Bpms == null || chart.Timing.Bpms.Count == 0)
                    {
                        throw new InvalidDataException($"Invalid chart file: Timing information is missing or incomplete in chart '{difficulty}'.");
                    }

                    // Chart의 bpmList는 이미 Timing 클래스에서 정렬됨
                }

                ParseNotes(chart);
            }

            return music;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"오류 발생: {ex.Message}");
            throw;
        }
    }

    private static void ParseNotes(Chart chart)
    {
        var rawNotes = chart.RawNotes;
        var parsedNotes = new List<Note>();

        foreach (var rawNote in rawNotes)
        {
            Note note = new Note();

            // 노트의 위치 계산
            int n = rawNote.N;
            note.Row = n / 4;
            note.Column = n % 4;

            int startTick = rawNote.T;

            // 시작 시간 계산
            note.StartTime = TickToSeconds(chart.Timing, startTick);

            // 롱노트 여부 판단
            if (rawNote.L.HasValue)
            {
                note.IsLongNote = true;

                int endTick = rawNote.L.Value;

                // 종료 시간 계산
                note.FinishTime = TickToSeconds(chart.Timing, endTick);

                // 화살표 위치 계산
                CalculateBarPosition(rawNote.P, note);
            }
            else
            {
                note.IsLongNote = false;
                note.FinishTime = note.StartTime;
            }

            parsedNotes.Add(note);
        }

        chart.Notes = parsedNotes;
    }

    private static double TickToSeconds(Timing timing, int noteTick)
    {
        double time = timing.Offset;
        int currentTick = 0;
        double currentBpm = timing.Bpms[0].Bpm; // 첫 번째 bpmEvent의 BPM을 초기 BPM으로 설정

        int bpmIndex = 0;

        while (bpmIndex < timing.Bpms.Count && timing.Bpms[bpmIndex].Tick <= noteTick)
        {
            var bpmEvent = timing.Bpms[bpmIndex];

            int deltaTick = bpmEvent.Tick - currentTick;
            double deltaTime = deltaTick * 60.0 / (currentBpm * timing.Resolution);
            time += deltaTime;

            currentTick = bpmEvent.Tick;
            currentBpm = bpmEvent.Bpm;

            bpmIndex++;
        }

        // 남은 틱 처리
        int remainingTick = noteTick - currentTick;
        double remainingTime = remainingTick * 60.0 / (currentBpm * timing.Resolution);
        time += remainingTime;

        return time;
    }

    private static void CalculateBarPosition(int p, Note note)
    {
        if (p >= 0 && p <= 2)
        {
            // 같은 행에서 노트의 열을 제외한 열 리스트 생성
            List<int> availableColumns = new List<int> { 0, 1, 2, 3 };
            availableColumns.Remove(note.Column);

            // p 값에 따른 바의 열 결정
            note.BarRow = note.Row;
            note.BarColumn = availableColumns[p];
        }
        else if (p >= 3 && p <= 5)
        {
            // 같은 열에서 노트의 행을 제외한 행 리스트 생성
            List<int> availableRows = new List<int> { 0, 1, 2, 3 };
            availableRows.Remove(note.Row);

            // p 값에 따른 바의 행 결정
            note.BarRow = availableRows[p - 3];
            note.BarColumn = note.Column;
        }
        else
        {
            // TODO: 파싱 에러 예외 발생
        }
    }
}
