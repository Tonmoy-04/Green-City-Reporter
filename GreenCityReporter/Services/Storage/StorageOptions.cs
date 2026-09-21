namespace GreenCityReporter.Services.Storage
{
    public sealed class StorageOptions
    {
        public string Provider { get; set; } = "Local";
        public SupabaseStorageOptions Supabase { get; set; } = new();
    }

    public sealed class SupabaseStorageOptions
    {
        public string Url { get; set; } = string.Empty;
        public string ServiceRoleKey { get; set; } = string.Empty;
        public string Bucket { get; set; } = "report-images";
    }
}