using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Umbraco.Core.IO;

namespace Umbraco.S3fs
{
    public class S3FileSystem : IFileSystem
    {
        #region Private Members

        string _bucketName;
        string _bucketHostName;
        string _bucketPrefix;
        string _accessKey;
        string _secretKey;
        string _region;

        #endregion

        #region Constants

        protected const string Delimiter = "/";
        protected const int BatchSize = 1000;

        #endregion

        #region Constructors

        public S3FileSystem(string bucketName, string bucketHostName, string bucketKeyPrefix, string region, string accessKey, string secretKey)
        {
            if (string.IsNullOrEmpty(bucketName))
                throw new ArgumentNullException("bucketName");
            if (string.IsNullOrEmpty(bucketHostName))
                throw new ArgumentNullException("bucketHostName");

            _bucketName = bucketName;
            _bucketHostName = ParseBucketHostName(bucketHostName);
            _bucketPrefix = ParseBucketPrefix(bucketKeyPrefix);
            _region = region;
            _accessKey = accessKey;
            _secretKey = secretKey;
        }

        #endregion

        #region Public Methods (IFileSystem)

        public IEnumerable<string> GetDirectories(string path)
        {
            var request = new ListObjectsRequest
            {
                BucketName = _bucketName,
                Delimiter = Delimiter,
                Prefix = ResolveBucketPath(path)
            };

            var response = ExecuteWithContinuation(request);
            return response
                .SelectMany(p => p.CommonPrefixes)
                .Select(p => string.Concat(p, Delimiter))
                .ToArray();
        }

        public void DeleteDirectory(string path)
        {
            DeleteDirectory(path, false);
        }

        public void DeleteDirectory(string path, bool recursive)
        {
            //TODO recursive (use WithDelimiter)
            //List Objects To Delete
            var listRequest = new ListObjectsRequest
            {
                BucketName = _bucketName,
                Prefix = ResolveBucketPath(path)
            };

            var listResponse = ExecuteWithContinuation(listRequest);
            var keys = listResponse
                .SelectMany(p => p.S3Objects)
                .Select(p => new KeyVersion { Key = p.Key })
                .ToArray();

            var deleteRequest = new DeleteObjectsRequest
            {
                BucketName = _bucketName,
                Objects = keys.ToList()
            };
            Execute(client => client.DeleteObjects(deleteRequest));
        }

        public bool DirectoryExists(string path)
        {
            var request = new ListObjectsRequest
            {
                BucketName = _bucketName,
                Prefix = ResolveBucketPath(path),
                MaxKeys = 1
            };

            var response = Execute(client => client.ListObjects(request));
            return response.S3Objects.Count > 0;
        }

        public void AddFile(string path, Stream stream)
        {
            AddFile(path, stream, true);
        }

        public void AddFile(string path, Stream stream, bool overrideIfExists)
        {
            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);

                var request = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = ResolveBucketPath(path),
                    CannedACL = S3CannedACL.PublicRead,
                    ContentType = "application/octet-stream",
                    InputStream = memoryStream
                };

                Execute(client => client.PutObject(request));
            }
        }

        public IEnumerable<string> GetFiles(string path)
        {
            return GetFiles(path, string.Empty);
        }

        public IEnumerable<string> GetFiles(string path, string filter)
        {
            var request = new ListObjectsRequest
            {
                BucketName = _bucketName,
                Delimiter = Delimiter,
                Prefix = ResolveBucketPath(path),
            };

            var response = ExecuteWithContinuation(request);
            return response.SelectMany(p => p.S3Objects).Select(p => p.Key);
        }

        public Stream OpenFile(string path)
        {
            var request = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = ResolveBucketPath(path)
            };

            var response = Execute(client => client.GetObject(request));

            //Read Response In Memory To Seek
            var stream = new MemoryStream();
            response.ResponseStream.CopyTo(stream);
            return stream;
        }

        public void DeleteFile(string path)
        {
            var request = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = ResolveBucketPath(path)
            };
            Execute(client => client.DeleteObject(request));
        }

        public bool FileExists(string path)
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = ResolveBucketPath(path)
            };

            try
            {
                Execute(client => client.GetObjectMetadata(request));
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
        }

        public string GetRelativePath(string fullPathOrUrl)
        {
            if (string.IsNullOrEmpty(fullPathOrUrl))
                return string.Empty;

            if (fullPathOrUrl.StartsWith("http")) { return fullPathOrUrl; }

            return GetUrl(fullPathOrUrl);
        }

        public string GetFullPath(string path)
        {
            return GetUrl(path);
        }

        public string GetUrl(string path)
        {
            return string.Concat(_bucketHostName, ResolveBucketPath(path));
        }

        public DateTimeOffset GetLastModified(string path)
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = ResolveBucketPath(path)
            };

            var response = Execute(client => client.GetObjectMetadata(request));
            return new DateTimeOffset(response.LastModified);
        }

        public DateTimeOffset GetCreated(string path)
        {
            return GetLastModified(path);
        }

        #endregion

        #region Private Methods

        AmazonS3Client GetClient()
        {
            return new AmazonS3Client(new BasicAWSCredentials(_accessKey, _secretKey), RegionEndpoint.GetBySystemName(_region));
        }

        T Execute<T>(Func<AmazonS3Client, T> request)
        {
            using (var client = GetClient())
            {
                try
                {
                    return request(client);
                }
                catch (AmazonS3Exception ex)
                {
                    if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        throw new FileNotFoundException(ex.Message);
                    }
                    throw;
                }
            }
        }

        IEnumerable<ListObjectsResponse> ExecuteWithContinuation(ListObjectsRequest request)
        {
            var response = Execute(client => client.ListObjects(request));
            yield return response;

            while (response.IsTruncated)
            {
                request.Marker = response.NextMarker;
                response = Execute(client => client.ListObjects(request));
                yield return response;
            }
        }

        string ResolveBucketPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return _bucketPrefix;
            if (path.StartsWith(_bucketHostName, StringComparison.InvariantCultureIgnoreCase))
                path = path.Substring(_bucketHostName.Length);

            path = path.Replace("\\", "/");
            if (path == "/")
                return _bucketPrefix;

            if (path.StartsWith("/"))
                path = path.Substring(1);

            return string.Concat(_bucketPrefix, path);
        }

        #region Static

        static string ParseBucketHostName(string hostname)
        {
            var ret = hostname.EndsWith("/")
                ? hostname
                : string.Concat(hostname, "/");

            if (!ret.StartsWith("http"))
            {
                ret = "http://" + ret;
            }

            return ret;
        }

        static string ParseBucketPrefix(string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
                return string.Empty;
            prefix = prefix.Replace("\\", "/");
            if (prefix == "/")
                return string.Empty;
            if (prefix.StartsWith("/"))
                prefix = prefix.Substring(1);
            return prefix.EndsWith("/")
                ? prefix
                : string.Concat(prefix, "/");
        }

        #endregion

        #endregion
    }
}