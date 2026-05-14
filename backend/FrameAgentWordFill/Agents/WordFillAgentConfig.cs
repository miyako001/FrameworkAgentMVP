namespace FrameAgentWordFill.Agents;

public static class WordFillAgentConfig
{
    public const string WelcomeMessageTemplate = "Welcome! Template: {TemplateName}, Fields: {FieldCount}{TableInfo}. {FirstQuestion}";

    public const string FieldExtractionPrompt = "Extract field values from user message. Return JSON only. User message: {USER_MESSAGE}. Fields: {FIELD_LIST}";

    public const string ShortcutDetectionPrompt = "Detect shortcut commands. User input: {USER_MESSAGE}. Return JSON with hasShortcut boolean.";

    public const string TableExtractionPrompt = "Extract table data from user message. Return JSON array. Message: {USER_MESSAGE}. Table: {TABLE_NAME}, Columns: {COLUMN_LIST}";
}
