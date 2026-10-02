using Amazon.S3;
using Amazon.S3.Model;

namespace MyStreamHistory.ContentService.Api;

public sealed class MediaStorage(IAmazonS3 client, ContentStorageOptions options)
{
    public async Task PutAsync(MediaAsset asset, Stream input, CancellationToken cancellationToken)
    {
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = options.Bucket,
            Key = asset.ObjectKey,
            InputStream = input,
            ContentType = asset.ContentType,
            AutoCloseStream = false
        }, cancellationToken);
    }

    public Task<GetObjectResponse> OpenAsync(MediaAsset asset, CancellationToken cancellationToken) =>
        client.GetObjectAsync(options.Bucket, asset.ObjectKey, cancellationToken);

    public Task DeleteAsync(MediaAsset asset, CancellationToken cancellationToken) => client.DeleteObjectAsync(
        options.Bucket, asset.ObjectKey, cancellationToken);
}
