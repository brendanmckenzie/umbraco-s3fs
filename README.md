umbraco-s3fs
============

A (very) simple S3 filesystem provider for Umbraco.

Only tested on Umbraco 7.1.4.

Used [Umbraco.Storage.S3](https://github.com/ElijahGlover/Umbraco-S3-Provider/tree/master/Umbraco.Storage.S3) as a basis.

## Installation

Modify your `config\FileSystemProviders.config` file.

    <Provider alias="media" type="Umbraco.S3fs.S3FileSystem, Umbraco.S3fs">
      <Parameters>
        <add key="bucketName" value="__BUCKET_NAME__" />
        <add key="bucketHostName" value="__BUCKET_HOST_NAME__" />
        <add key="bucketKeyPrefix" value="__BUCKET_KEY_PREFIX__" />
        <add key="region" value="__AWS_REGION__" />
        <add key="accessKey" value="__ACCESS_KEY__" />
        <add key="secretKey" value="__SECRET_KEY__" />
      </Parameters>
    </Provider>

* **__BUCKET_NAME__** the S3 bucket to use
* **__BUCKET_HOST_NAME__** the hostname to prefix URLs with
* **__BUCKET_KEY_PREFIX__** what to prefix the uploaded files with (can be blank)
* **__AWS_REGION__** The AWS region your S3 bucket is hosted in [http://docs.aws.amazon.com/general/latest/gr/rande.html#s3_region]()
* **__ACCESS_KEY__** Your AWS access key (preferably configured using IAM)
* **__SECRET_KEY__** Your AWS secret key

## AWS Configuration

This is the bucket policy I have configured for my setup.

    {
      "Version": "2008-10-17",
      "Id": "Policy1414709594921",
      "Statement": [
        {
          "Sid": "Stmt1414709562475",
          "Effect": "Allow",
          "Principal": {
            "AWS": "__AWS_IAM_USER_ARN__"
          },
          "Action": [
            "s3:DeleteObject",
            "s3:*",
            "s3:PutObject"
          ],
          "Resource": "arn:aws:s3:::__BUCKET_NAME__/*"
        },
        {
          "Sid": "Stmt1414709562475",
          "Effect": "Allow",
          "Principal": {
            "AWS": "__AWS_IAM_USER_ARN__"
          },
          "Action": "s3:ListBucket",
          "Resource": "arn:aws:s3:::__BUCKET_NAME__"
        },
        {
          "Sid": "Stmt1414709591353",
          "Effect": "Allow",
          "Principal": {
            "AWS": "*"
          },
          "Action": "s3:GetObject",
          "Resource": "arn:aws:s3:::__BUCKET_NAME__/*"
        }
      ]
    }