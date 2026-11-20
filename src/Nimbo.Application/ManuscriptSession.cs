using Nimbo.Core;
using System.Text.Json;

namespace Nimbo.Application;

public interface IWorkRepository
{
    (Work Work, long Revision) Open(string id);
    long Save(Work work, long expectedRevision);
}

public sealed class ManuscriptSession(IWorkRepository repository)
{
    public Work? Current { get; private set; }
    public long Revision { get; private set; }
    public bool HasChanges { get; private set; }
    public void Open(Work work, long revision)
    {
        Current = work; Revision = revision; HasChanges = false;
    }
    public void Close()
    {
        Save();
        Current = null;
    }
    public void MarkChanged() => HasChanges = Current != null;
    public void Save()
    {
        if (Current == null || !HasChanges) return;
        Current.ModifiedAt = DateTimeOffset.Now.ToString("O");
        Revision = repository.Save(Current, Revision);
        HasChanges = false;
    }
    public Work Snapshot() => JsonSerializer.Deserialize<Work>(JsonSerializer.Serialize(Current ?? throw new InvalidOperationException("Abre una obra.")))!;
    public Work SaveCopy()
    {
        var copy = Snapshot();
        copy.Id = Guid.NewGuid().ToString(); copy.Title += " (copia)";
        Open(copy, repository.Save(copy, 0)); return copy;
    }
}
