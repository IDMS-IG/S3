using System;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Amazon.S3.Util;
using System.IO;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.IO.Compression;
using NLog.Extensions.Logging;
using NLog;
using awslib;

namespace S3
{
    class Program
    {
        private static IConfigurationRoot configuration;
        private static Logger logger;
        static void Main(string[] args)
        {
            configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            CreateLogger();
            logger.Info("Process Started");

            var c = new S3Client(configuration, args[0]);
            var s3Service = new S3Service(c.s3Client, c.BucketName, c.BucketRegion, configuration, logger);

            var action = args[1].ToUpper();

            if (action == "LIST")
            {
                var files = s3Service.ListFiles(args.Length > 2 ? args[2] : "");
                foreach (var f in files.OrderByDescending(x => x.LastModified))
                    Console.WriteLine($"File Name: {f.Key} Size: {f.Size / 1024 / 1024} Modified Date: {f.LastModified}");

                Console.WriteLine($"Total Files:{files.Count}");
            }

            if (action == "UPLOAD")
                s3Service.UploadFiles(args);

            if (action == "DOWNLOAD")
                s3Service.DownloadFiles(args[2], args.Length > 3 ? args[3] : "");

            if (action == "TRANSFER")
                s3Service.TransferFilesBetweenS3(args);

            if (action == "COPY")
                s3Service.CopyFilesBetweenFolders(args);

            if (action == "DELETE")
                s3Service.DeleteFiles(args);

            if (action == "AI")
                ProcessAIModelFiles(s3Service, args);

            logger.Info("Process Ended.");
        }

        private static void CreateLogger()
        {
            var config = new ConfigurationBuilder()
                   .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                   .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                   .Build();

            LogManager.Configuration = new NLogLoggingConfiguration(config.GetSection("NLog"));
            logger = LogManager.GetCurrentClassLogger();
        }

        private static void ProcessAIModelFiles(S3Service s3Service, string[] args)
        {
            try
            {
                var downloadPath = configuration["AI_DOWNLOAD_PATH"];
                var utils = new Utilities();
                var files = s3Service.ListFiles("idms/AI");
                logger.Info($"Total AI Model Files to download:{files.Count}");
                foreach (var f in files)
                {
                    var targetFile = Path.Combine("idms-processed/", Path.GetFileName(f.Key));

                    s3Service.DownloadFile(f.Key, downloadPath);
                    utils.Decompress(Path.Combine(downloadPath + @"idms\", Path.GetFileName(f.Key)), Path.Combine(downloadPath, Path.GetFileNameWithoutExtension(f.Key) + ".gz"));
                    s3Service.CopyingObjectAsync(f.Key, targetFile).Wait();
                    s3Service.DeleteObjectNonVersionedBucketAsync(f.Key).Wait();
                    logger.Info($"AI Model File {f.Key} downloaded.");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error Downloding AI Model File {ex}");
                throw ex;
            }
        }
    }
}
