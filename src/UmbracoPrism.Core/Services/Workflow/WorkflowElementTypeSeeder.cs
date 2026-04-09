using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;

namespace UmbracoPrism.Core.Services.Workflow;

/// <summary>
/// Creates Umbraco Element Types for workflow step definitions.
/// Runs idempotently — only creates element types if they don't already exist.
/// </summary>
public class WorkflowElementTypeSeeder(
    IContentTypeService contentTypeService,
    IDataTypeService dataTypeService,
    IShortStringHelper shortStringHelper,
    IConfigurationEditorJsonSerializer configurationEditorJsonSerializer,
    PropertyEditorCollection propertyEditorCollection,
    ILogger<WorkflowElementTypeSeeder> logger)
{
    // Fixed GUIDs for deterministic seeding
    private static readonly Guid WorkflowTextStringDataTypeKey = new("1a2b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d");
    private static readonly Guid WorkflowEmailAddressDataTypeKey = new("2b3c4d5e-6f7a-8b9c-0d1e-2f3a4b5c6d7e");
    private static readonly Guid WorkflowDateTimeDataTypeKey = new("3c4d5e6f-7a8b-9c0d-1e2f-3a4b5c6d7e8f");
    private static readonly Guid WorkflowIntegerDataTypeKey = new("4d5e6f7a-8b9c-0d1e-2f3a-4b5c6d7e8f9a");
    private static readonly Guid WorkflowTrueFalseDataTypeKey = new("5e6f7a8b-9c0d-1e2f-3a4b-5c6d7e8f9a0b");

    /// <summary>
    /// Ensures all workflow element types exist.
    /// </summary>
    public async Task EnsureElementTypesAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("WorkflowElementTypeSeeder: Starting");

        await EnsureWorkflowPersonalDetailsAsync(cancellationToken);
        await EnsureWorkflowFinancialDetailsAsync(cancellationToken);

        logger.LogInformation("WorkflowElementTypeSeeder: Complete");
    }

    private async Task EnsureWorkflowPersonalDetailsAsync(CancellationToken cancellationToken)
    {
        const string alias = "workflowPersonalDetails";
        const string name = "Workflow: Personal Details";

        var contentType = contentTypeService.Get(alias);
        if (contentType != null)
        {
            logger.LogDebug("WorkflowElementTypeSeeder: Element type '{Alias}' already exists", alias);
            return;
        }

        logger.LogInformation("WorkflowElementTypeSeeder: Creating element type '{Alias}'", alias);

        contentType = new ContentType(shortStringHelper, -1)
        {
            Alias = alias,
            Name = name,
            IsElement = true,
            Icon = "icon-user"
        };

        // Create property group
        const string groupName = "Personal Details";
        const string groupKey = "personalDetails";
        contentType.AddPropertyGroup(groupName, groupKey);

        // Get or create data types
        var textStringDataType = await GetOrCreateTextStringDataTypeAsync();
        var emailDataType = await GetOrCreateEmailAddressDataTypeAsync();
        var dateTimeDataType = await GetOrCreateDateTimeDataTypeAsync();

        if (textStringDataType == null || emailDataType == null || dateTimeDataType == null)
        {
            logger.LogError("WorkflowElementTypeSeeder: Failed to create data types for '{Alias}'", alias);
            return;
        }

        // Add properties
        var firstName = new PropertyType(shortStringHelper, textStringDataType, "firstName")
        {
            Name = "First name",
            Mandatory = true,
            SortOrder = 0
        };
        contentType.AddPropertyType(firstName, groupName);

        var lastName = new PropertyType(shortStringHelper, textStringDataType, "lastName")
        {
            Name = "Last name",
            Mandatory = true,
            SortOrder = 1
        };
        contentType.AddPropertyType(lastName, groupName);

        var email = new PropertyType(shortStringHelper, emailDataType, "email")
        {
            Name = "Email address",
            Mandatory = true,
            SortOrder = 2
        };
        contentType.AddPropertyType(email, groupName);

        var dateOfBirth = new PropertyType(shortStringHelper, dateTimeDataType, "dateOfBirth")
        {
            Name = "Date of birth",
            Mandatory = false,
            SortOrder = 3
        };
        contentType.AddPropertyType(dateOfBirth, groupName);

#pragma warning disable CS0618
        contentTypeService.Save(contentType);
#pragma warning restore CS0618

        logger.LogInformation("WorkflowElementTypeSeeder: Created element type '{Alias}'", alias);
    }

    private async Task EnsureWorkflowFinancialDetailsAsync(CancellationToken cancellationToken)
    {
        const string alias = "workflowFinancialDetails";
        const string name = "Workflow: Financial Details";

        var contentType = contentTypeService.Get(alias);
        if (contentType != null)
        {
            logger.LogDebug("WorkflowElementTypeSeeder: Element type '{Alias}' already exists", alias);
            return;
        }

        logger.LogInformation("WorkflowElementTypeSeeder: Creating element type '{Alias}'", alias);

        contentType = new ContentType(shortStringHelper, -1)
        {
            Alias = alias,
            Name = name,
            IsElement = true,
            Icon = "icon-coins-pound-sterling"
        };

        // Create property group
        const string groupName = "Financial Details";
        const string groupKey = "financialDetails";
        contentType.AddPropertyGroup(groupName, groupKey);

        // Get or create data types
        var textStringDataType = await GetOrCreateTextStringDataTypeAsync();
        var integerDataType = await GetOrCreateIntegerDataTypeAsync();
        var trueFalseDataType = await GetOrCreateTrueFalseDataTypeAsync();

        if (textStringDataType == null || integerDataType == null || trueFalseDataType == null)
        {
            logger.LogError("WorkflowElementTypeSeeder: Failed to create data types for '{Alias}'", alias);
            return;
        }

        // Add properties
        var annualIncome = new PropertyType(shortStringHelper, integerDataType, "annualIncome")
        {
            Name = "Annual income (£)",
            Mandatory = true,
            SortOrder = 0
        };
        contentType.AddPropertyType(annualIncome, groupName);

        var employerName = new PropertyType(shortStringHelper, textStringDataType, "employerName")
        {
            Name = "Employer name",
            Mandatory = false,
            SortOrder = 1
        };
        contentType.AddPropertyType(employerName, groupName);

        var taxResident = new PropertyType(shortStringHelper, trueFalseDataType, "taxResident")
        {
            Name = "UK tax resident?",
            Description = "Are you a UK tax resident?",
            Mandatory = false,
            SortOrder = 2
        };
        contentType.AddPropertyType(taxResident, groupName);

#pragma warning disable CS0618
        contentTypeService.Save(contentType);
#pragma warning restore CS0618

        logger.LogInformation("WorkflowElementTypeSeeder: Created element type '{Alias}'", alias);
    }

    private async Task<IDataType?> GetOrCreateTextStringDataTypeAsync()
    {
        const string editorAlias = "Umbraco.TextBox";
        const string dataTypeName = "Workflow Text String";

        var existing = await dataTypeService.GetAsync(WorkflowTextStringDataTypeKey);
        if (existing != null) return existing;

        var editor = propertyEditorCollection[editorAlias];
        if (editor == null)
        {
            logger.LogError("WorkflowElementTypeSeeder: Editor '{EditorAlias}' not found", editorAlias);
            return null;
        }

        var dataType = new DataType(editor, configurationEditorJsonSerializer)
        {
            Key = WorkflowTextStringDataTypeKey,
            Name = dataTypeName,
            DatabaseType = ValueStorageType.Nvarchar,
            EditorUiAlias = "Umb.PropertyEditorUi.TextBox"
        };

        await dataTypeService.CreateAsync(dataType, Constants.Security.SuperUserKey);
        return dataType;
    }

    private async Task<IDataType?> GetOrCreateEmailAddressDataTypeAsync()
    {
        const string editorAlias = "Umbraco.EmailAddress";
        const string dataTypeName = "Workflow Email Address";

        var existing = await dataTypeService.GetAsync(WorkflowEmailAddressDataTypeKey);
        if (existing != null) return existing;

        var editor = propertyEditorCollection[editorAlias];
        if (editor == null)
        {
            logger.LogError("WorkflowElementTypeSeeder: Editor '{EditorAlias}' not found", editorAlias);
            return null;
        }

        var dataType = new DataType(editor, configurationEditorJsonSerializer)
        {
            Key = WorkflowEmailAddressDataTypeKey,
            Name = dataTypeName,
            DatabaseType = ValueStorageType.Nvarchar,
            EditorUiAlias = "Umb.PropertyEditorUi.EmailAddress"
        };

        await dataTypeService.CreateAsync(dataType, Constants.Security.SuperUserKey);
        return dataType;
    }

    private async Task<IDataType?> GetOrCreateDateTimeDataTypeAsync()
    {
        const string editorAlias = "Umbraco.DateTime";
        const string dataTypeName = "Workflow Date Time";

        var existing = await dataTypeService.GetAsync(WorkflowDateTimeDataTypeKey);
        if (existing != null) return existing;

        var editor = propertyEditorCollection[editorAlias];
        if (editor == null)
        {
            logger.LogError("WorkflowElementTypeSeeder: Editor '{EditorAlias}' not found", editorAlias);
            return null;
        }

        var dataType = new DataType(editor, configurationEditorJsonSerializer)
        {
            Key = WorkflowDateTimeDataTypeKey,
            Name = dataTypeName,
            DatabaseType = ValueStorageType.Date,
            EditorUiAlias = "Umb.PropertyEditorUi.DatePicker"
        };

        await dataTypeService.CreateAsync(dataType, Constants.Security.SuperUserKey);
        return dataType;
    }

    private async Task<IDataType?> GetOrCreateIntegerDataTypeAsync()
    {
        const string editorAlias = "Umbraco.Integer";
        const string dataTypeName = "Workflow Integer";

        var existing = await dataTypeService.GetAsync(WorkflowIntegerDataTypeKey);
        if (existing != null) return existing;

        var editor = propertyEditorCollection[editorAlias];
        if (editor == null)
        {
            logger.LogError("WorkflowElementTypeSeeder: Editor '{EditorAlias}' not found", editorAlias);
            return null;
        }

        var dataType = new DataType(editor, configurationEditorJsonSerializer)
        {
            Key = WorkflowIntegerDataTypeKey,
            Name = dataTypeName,
            DatabaseType = ValueStorageType.Integer,
            EditorUiAlias = "Umb.PropertyEditorUi.Integer"
        };

        await dataTypeService.CreateAsync(dataType, Constants.Security.SuperUserKey);
        return dataType;
    }

    private async Task<IDataType?> GetOrCreateTrueFalseDataTypeAsync()
    {
        const string editorAlias = "Umbraco.TrueFalse";
        const string dataTypeName = "Workflow True/False";

        var existing = await dataTypeService.GetAsync(WorkflowTrueFalseDataTypeKey);
        if (existing != null) return existing;

        var editor = propertyEditorCollection[editorAlias];
        if (editor == null)
        {
            logger.LogError("WorkflowElementTypeSeeder: Editor '{EditorAlias}' not found", editorAlias);
            return null;
        }

        var dataType = new DataType(editor, configurationEditorJsonSerializer)
        {
            Key = WorkflowTrueFalseDataTypeKey,
            Name = dataTypeName,
            DatabaseType = ValueStorageType.Integer,
            EditorUiAlias = "Umb.PropertyEditorUi.Toggle"
        };

        await dataTypeService.CreateAsync(dataType, Constants.Security.SuperUserKey);
        return dataType;
    }
}
