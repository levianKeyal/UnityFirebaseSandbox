using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "StudentDataRegistry", menuName = "Student Data/Registry")]
public class StudentDataRegistry : ScriptableObject
{
    [SerializeField] private List<StudentDataBlockDefinition> blocks = new List<StudentDataBlockDefinition>();
    [SerializeField] private List<StudentDataDefinition> definitions = new List<StudentDataDefinition>();

    public IReadOnlyList<StudentDataBlockDefinition> Blocks => blocks;
    public IReadOnlyList<StudentDataDefinition> Definitions => definitions;

    public StudentDataValidationResult Validate()
    {
        StudentDataValidationResult result = new StudentDataValidationResult();
        Dictionary<string, StudentDataBlockDefinition> blockKeys =
            new Dictionary<string, StudentDataBlockDefinition>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, StudentDataDefinition> dataKeys =
            new Dictionary<string, StudentDataDefinition>(StringComparer.OrdinalIgnoreCase);
        HashSet<StudentDataBlockDefinition> registeredBlocks = new HashSet<StudentDataBlockDefinition>();
        HashSet<StudentDataDefinition> registeredDefinitions = new HashSet<StudentDataDefinition>();

        for (int index = 0; index < blocks.Count; index++)
        {
            StudentDataBlockDefinition block = blocks[index];
            if (block == null)
            {
                result.AddError($"Block entry {index} is null.");
                continue;
            }

            if (!registeredBlocks.Add(block))
            {
                result.AddError($"Block definition is registered more than once: {block.name}.");
            }

            AddErrors(result, block.Validate(), $"Block '{block.name}'");
            if (!string.IsNullOrWhiteSpace(block.Key))
            {
                StudentDataBlockDefinition previousBlock;
                if (blockKeys.TryGetValue(block.Key, out previousBlock))
                {
                    result.AddError(
                        $"Duplicate block key '{block.Key}': "
                        + $"{DescribeAsset(previousBlock, blocks.IndexOf(previousBlock))} conflicts with "
                        + $"{DescribeAsset(block, index)}."
                    );
                }
                else
                {
                    blockKeys.Add(block.Key, block);
                }
            }
        }

        for (int index = 0; index < definitions.Count; index++)
        {
            StudentDataDefinition definition = definitions[index];
            if (definition == null)
            {
                result.AddError($"Data definition entry {index} is null.");
                continue;
            }

            if (!registeredDefinitions.Add(definition))
            {
                result.AddError($"Data definition is registered more than once: {definition.name}.");
            }

            AddErrors(result, definition.Validate(), $"Data '{definition.name}'");

            if (!string.IsNullOrWhiteSpace(definition.Key))
            {
                StudentDataDefinition previousDefinition;
                if (dataKeys.TryGetValue(definition.Key, out previousDefinition))
                {
                    result.AddError(
                        $"Duplicate data key '{definition.Key}': "
                        + $"{DescribeAsset(previousDefinition, definitions.IndexOf(previousDefinition))} conflicts with "
                        + $"{DescribeAsset(definition, index)}."
                    );
                }
                else
                {
                    dataKeys.Add(definition.Key, definition);
                }
            }

            if (definition.Block != null && !registeredBlocks.Contains(definition.Block))
            {
                result.AddError(
                    $"Data definition '{definition.Key}' references a block not registered in this registry."
                );
            }
        }

        return result;
    }

    public bool IsValid()
    {
        return Validate().IsValid;
    }

    private static void AddErrors(
        StudentDataValidationResult target,
        StudentDataValidationResult source,
        string context
    )
    {
        foreach (string error in source.Errors)
        {
            target.AddError($"{context}: {error}");
        }

        foreach (string warning in source.Warnings)
        {
            target.AddWarning($"{context}: {warning}");
        }
    }

    private static string DescribeAsset(ScriptableObject asset, int index)
    {
        return $"'{asset.name}' (registry index {index})";
    }
}
