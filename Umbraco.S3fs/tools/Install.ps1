param($installPath, $toolsPath, $package, $project)

function AddHelperText {
	$item = ((Get-Item $project.FileName).Directory).FullName + "\config\FileSystemProviders.config"
	Write-Host $item

	$file = $item

	Write-Host "Updating " $file " ..."

	$find = '</FileSystemProviders>'

	$replace = '  <!-- Example Umbraco.S3fs Configuration:
  <Provider alias="media" type="Umbraco.S3fs.S3FileSystem, Umbraco.S3fs">
    <Parameters>
      <add key="bucketName" value="__BUCKET_NAME__" />
      <add key="bucketHostName" value="__BUCKET_HOST_NAME__" />
      <add key="bucketKeyPrefix" value="" />
      <add key="region" value="__AWS_REGION__" />
      <add key="accessKey" value="__ACCESS_KEY__" />
      <add key="secretKey" value="__SECRET_KEY__" />
    </Parameters>
  </Provider>
  -->
</FileSystemProviders>'

	(gc $file).replace($find, $replace) | sc $file

}

AddHelperText