using CalendarWidget.Models;

namespace CalendarWidget.Services;

public sealed class TaskStore
{
    private const string FileName = "tasks.json";

    public List<CalendarTask> Load() => AppDataFile.Read<List<CalendarTask>>(FileName) ?? [];

    public void Save(IEnumerable<CalendarTask> tasks) => AppDataFile.Write(FileName, tasks);
}
