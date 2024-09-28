namespace mypetpal.Data.Common.Interface
{
    public interface IMetadata
        {         
            DateTime? Metadata_createdUtc { get; set; }

            DateTime? Metadata_deletedUtc { get; set; }

            DateTime? Metadata_updatedUtc { get; set; }
        }
    
}
