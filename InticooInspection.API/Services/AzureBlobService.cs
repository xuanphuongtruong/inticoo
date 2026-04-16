using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace InticooInspection.API.Services
{
    public class AzureBlobService
    {
        private readonly BlobContainerClient _container;

        public AzureBlobService(IConfiguration config)
        {
            var connStr = config["AzureStorage:ConnectionString"];
            var containerName = config["AzureStorage:ContainerName"];
            _container = new BlobContainerClient(connStr, containerName);
            _container.CreateIfNotExists(PublicAccessType.Blob);
        }

        public async Task<string> UploadAsync(string folder, string fileName, Stream stream, string contentType)
        {
            var blobPath = $"{folder}/{fileName}";
            var blobClient = _container.GetBlobClient(blobPath);
            stream.Position = 0;
            await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = contentType });
            return blobClient.Uri.ToString(); // https://....blob.core.windows.net/uploads/photos/abc.jpg
        }

        public async Task DeleteAsync(string folder, string fileName)
        {
            var blobClient = _container.GetBlobClient($"{folder}/{fileName}");
            await blobClient.DeleteIfExistsAsync();
        }
    }
}