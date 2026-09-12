using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public sealed class StudentDataLoadCoordinator
{
    private readonly StudentDataBlockPersistenceService persistenceService;
    private readonly StudentDataValueSerializer valueSerializer;

    public StudentDataLoadCoordinator(
        StudentDataBlockPersistenceService persistenceService
    )
    {
        if (persistenceService == null)
        {
            throw new ArgumentNullException(nameof(persistenceService));
        }

        this.persistenceService = persistenceService;
        valueSerializer = new StudentDataValueSerializer();
    }

    public async Task<bool> LoadInitialDataAsync(
        string uid,
        StudentDataRegistry registry,
        StudentDataRuntimeStore runtimeStore
    )
    {
        if (string.IsNullOrWhiteSpace(uid)
            || registry == null
            || runtimeStore == null
            || !runtimeStore.IsInitialized
            || runtimeStore.Registry != registry)
        {
            return false;
        }

        Dictionary<StudentDataBlockDefinition, List<StudentDataDefinition>> definitionsByBlock =
            new Dictionary<StudentDataBlockDefinition, List<StudentDataDefinition>>();

        foreach (StudentDataDefinition definition in registry.Definitions)
        {
            if (!ShouldLoad(definition))
            {
                continue;
            }

            if (definition.Block == null)
            {
                return false;
            }

            List<StudentDataDefinition> blockDefinitions;
            if (!definitionsByBlock.TryGetValue(definition.Block, out blockDefinitions))
            {
                blockDefinitions = new List<StudentDataDefinition>();
                definitionsByBlock.Add(definition.Block, blockDefinitions);
            }

            blockDefinitions.Add(definition);
        }

        try
        {
            foreach (
                KeyValuePair<StudentDataBlockDefinition, List<StudentDataDefinition>> group
                in definitionsByBlock
            )
            {
                StudentDataBlockLoadResult loadResult =
                    await persistenceService.LoadBlockAsync(uid, group.Key);

                if (loadResult.IsNotFound)
                {
                    if (!ApplyDefaults(group.Value, runtimeStore))
                    {
                        return false;
                    }

                    continue;
                }

                if (!loadResult.Success || loadResult.Document == null)
                {
                    return false;
                }

                if (!ApplyDocumentValues(group.Value, loadResult.Document, runtimeStore))
                {
                    return false;
                }
            }
        }
        catch
        {
            return false;
        }

        return true;
    }

    private bool ApplyDocumentValues(
        IReadOnlyList<StudentDataDefinition> definitions,
        StudentDataBlockDocument document,
        StudentDataRuntimeStore runtimeStore
    )
    {
        foreach (StudentDataDefinition definition in definitions)
        {
            object storedValue;
            object valueToApply;
            if (document.StoredValues.TryGetValue(definition.Key, out storedValue))
            {
                string error;
                if (!valueSerializer.TryDeserialize(
                    definition,
                    storedValue,
                    out valueToApply,
                    out error
                ))
                {
                    return false;
                }
            }
            else if (!definition.TryGetDefaultValue(out valueToApply))
            {
                return false;
            }

            string applyError;
            if (!runtimeStore.TryApplyLoadedValue(definition, valueToApply, out applyError))
            {
                return false;
            }
        }

        return true;
    }

    private bool ApplyDefaults(
        IReadOnlyList<StudentDataDefinition> definitions,
        StudentDataRuntimeStore runtimeStore
    )
    {
        foreach (StudentDataDefinition definition in definitions)
        {
            object defaultValue;
            if (!definition.TryGetDefaultValue(out defaultValue))
            {
                return false;
            }

            string error;
            if (!runtimeStore.TryApplyLoadedValue(definition, defaultValue, out error))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ShouldLoad(StudentDataDefinition definition)
    {
        if (definition == null)
        {
            return false;
        }

        return definition.LoadPolicy == StudentDataLoadPolicy.RestoreOnLogin
            || definition.LoadPolicy == StudentDataLoadPolicy.LoadNormally;
    }
}
