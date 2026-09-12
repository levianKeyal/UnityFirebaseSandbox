using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public sealed class StudentDataSaveCoordinator
{
    private readonly StudentDataBlockPersistenceService persistenceService;
    private readonly Dictionary<StudentDataBlockDefinition, long> saveRevisions;

    public StudentDataSaveCoordinator(
        StudentDataBlockPersistenceService persistenceService
    )
    {
        if (persistenceService == null)
        {
            throw new ArgumentNullException(nameof(persistenceService));
        }

        this.persistenceService = persistenceService;
        saveRevisions = new Dictionary<StudentDataBlockDefinition, long>();
    }

    public async Task<bool> SaveDirtyDataAsync(
        string uid,
        StudentDataRuntimeStore runtimeStore
    )
    {
        if (string.IsNullOrWhiteSpace(uid) || runtimeStore == null || !runtimeStore.IsInitialized)
        {
            return false;
        }

        IReadOnlyList<StudentDataRuntimeSnapshot> dirtySnapshots =
            runtimeStore.GetDirtySnapshots();
        if (dirtySnapshots == null || dirtySnapshots.Count == 0)
        {
            return true;
        }

        Dictionary<StudentDataBlockDefinition, List<StudentDataRuntimeSnapshot>> snapshotsByBlock =
            new Dictionary<StudentDataBlockDefinition, List<StudentDataRuntimeSnapshot>>();
        bool allSucceeded = true;

        foreach (StudentDataRuntimeSnapshot snapshot in dirtySnapshots)
        {
            StudentDataBlockDefinition block = snapshot != null ? snapshot.Block : null;
            if (block == null)
            {
                allSucceeded = false;
                continue;
            }

            List<StudentDataRuntimeSnapshot> blockSnapshots;
            if (!snapshotsByBlock.TryGetValue(block, out blockSnapshots))
            {
                blockSnapshots = new List<StudentDataRuntimeSnapshot>();
                snapshotsByBlock.Add(block, blockSnapshots);
            }

            blockSnapshots.Add(snapshot);
        }

        foreach (KeyValuePair<StudentDataBlockDefinition, List<StudentDataRuntimeSnapshot>> group in snapshotsByBlock)
        {
            long saveRevision;
            if (!TryGetNextSaveRevision(group.Key, out saveRevision))
            {
                allSucceeded = false;
                continue;
            }

            StudentDataPersistenceResult persistenceResult;
            try
            {
                persistenceResult = await persistenceService.SaveDirtyBlockAsync(
                    uid,
                    group.Key,
                    group.Value,
                    saveRevision
                );
            }
            catch
            {
                allSucceeded = false;
                continue;
            }

            if (!persistenceResult.Success)
            {
                allSucceeded = false;
                continue;
            }

            foreach (StudentDataRuntimeSnapshot snapshot in group.Value)
            {
                string error;
                runtimeStore.TryMarkClean(
                    snapshot.Definition,
                    snapshot.Revision,
                    out error
                );
            }
        }

        return allSucceeded;
    }

    private bool TryGetNextSaveRevision(
        StudentDataBlockDefinition block,
        out long saveRevision
    )
    {
        long currentRevision;
        if (!saveRevisions.TryGetValue(block, out currentRevision))
        {
            saveRevision = 1;
            saveRevisions.Add(block, saveRevision);
            return true;
        }

        if (currentRevision == long.MaxValue)
        {
            saveRevision = 0;
            return false;
        }

        saveRevision = checked(currentRevision + 1);
        saveRevisions[block] = saveRevision;
        return true;
    }
}
