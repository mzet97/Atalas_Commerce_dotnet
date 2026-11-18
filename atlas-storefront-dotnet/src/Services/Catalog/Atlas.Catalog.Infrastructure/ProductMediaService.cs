using Amazon.S3;
using Amazon.S3.Model;
using Atlas.Catalog.Application;
using Atlas.Catalog.Domain;

namespace Atlas.Catalog.Infrastructure;

/// <summary>Opções do object storage (MinIO/S3): bucket atlas-catalog, leitura pública
/// em products/* (vitrine usa URL direta), escrita só via PUT pré-assinado do owner.</summary>
public sealed class MediaOptions
{
    public string ServiceUrl { get; set; } = "http://127.0.0.1:9000";
    /// <summary>Endpoint público para ASSINAR (o uploader é o browser, fora do cluster);
    /// a assinatura cobre o host — tem que ser o host que o browser vai chamar.</summary>
    public string PresignServiceUrl { get; set; } = "";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string Bucket { get; set; } = "atlas-catalog";
    public string PublicBaseUrl { get; set; } = "";
    public TimeSpan UploadTtl { get; set; } = TimeSpan.FromMinutes(10);
}

/// <summary>Adapter S3/MinIO do Catalog: presign PUT (bytes diretos browser→MinIO),
/// HEAD para validar no attach, ensure de bucket + política pública (idempotente).</summary>
public sealed class ProductMediaService : IMediaStorage, IDisposable
{
    private readonly MediaOptions _options;
    private readonly AmazonS3Client _s3; // data plane (HEAD, bucket) — endpoint interno
    private readonly AmazonS3Client _sign; // só assina PUT — endpoint PÚBLICO

    public ProductMediaService(MediaOptions options)
    {
        _options = options;
        _s3 = new AmazonS3Client(options.AccessKey, options.SecretKey, new AmazonS3Config
        {
            ServiceURL = options.ServiceUrl,
            ForcePathStyle = true,
        });
        _sign = new AmazonS3Client(options.AccessKey, options.SecretKey, new AmazonS3Config
        {
            ServiceURL = string.IsNullOrWhiteSpace(options.PresignServiceUrl) ? options.ServiceUrl : options.PresignServiceUrl,
            ForcePathStyle = true,
        });
    }

    public void Dispose()
    {
        _s3.Dispose();
        _sign.Dispose();
    }

    /// <summary>Cria bucket + política de leitura pública em products/* (idempotente).</summary>
    public async Task EnsureBucketAsync(CancellationToken ct)
    {
        // HeadBucket em vez de ListBuckets: MinIO/SDK podem devolver Buckets nulo.
        try
        {
            await _s3.HeadBucketAsync(new HeadBucketRequest { BucketName = _options.Bucket }, ct);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            await _s3.PutBucketAsync(new PutBucketRequest { BucketName = _options.Bucket }, ct);
        }
        // Leitura pública só do prefixo de produto (vitrine <img> sem assinar).
        var policy = $$"""
            {"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"AWS":["*"]},"Action":["s3:GetObject"],"Resource":["arn:aws:s3:::{{_options.Bucket}}/products/*"]}]}
            """;
        await _s3.PutBucketPolicyAsync(new PutBucketPolicyRequest { BucketName = _options.Bucket, Policy = policy }, ct);
    }

    /// <summary>Assina PUT direto (valida tipo/tamanho ANTES — regras de ProductImage).</summary>
    public Task<PresignedUpload> PresignPutAsync(ProductId productId, string contentType, long contentLength, CancellationToken ct)
    {
        if (!ProductImage.AllowedContentTypes.Contains(contentType))
            throw new ArgumentException($"Tipo não permitido: '{contentType}' (jpeg/png/webp)", nameof(contentType));
        if (contentLength <= 0 || contentLength > ProductImage.MaxSizeBytes)
            throw new ArgumentException($"Tamanho inválido: {contentLength} (teto {ProductImage.MaxSizeBytes})", nameof(contentLength));
        var key = $"products/{productId}/{Guid.NewGuid():N}{ProductImage.ExtensionFor(contentType)}";
        var url = _sign.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            ContentType = contentType, // assinado: MinIO rejeita Content-Type divergente
            Expires = DateTime.UtcNow.Add(_options.UploadTtl),
        });
        return Task.FromResult(new PresignedUpload(url, key, DateTimeOffset.UtcNow.Add(_options.UploadTtl)));
    }

    /// <summary>HEAD do objeto (attach só do que existe, com tipo/tamanho íntegros).</summary>
    public async Task<ObjectMetadata?> HeadObjectAsync(string key, CancellationToken ct)
    {
        try
        {
            var meta = await _s3.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = _options.Bucket,
                Key = key,
            }, ct);
            return new ObjectMetadata(meta.Headers.ContentType, meta.ContentLength);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
