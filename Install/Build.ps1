﻿cd "$PSScriptRoot" 
& "$PSScriptRoot\CopyFiles.ps1"

& ".\signtool.exe" sign /v /n "West Wind Technologies" /sm /s MY /tr "http://timestamp.digicert.com" /td SHA256 /fd SHA256 ".\Distribution\MarkdownMonster.exe"
& ".\signtool.exe" sign /v /n "West Wind Technologies" /sm /s MY /tr "http://timestamp.digicert.com" /td SHA256 /fd SHA256 ".\Distribution\mm.exe"

"Running Inno Setup..."
& "C:\Program Files (x86)\Inno Setup 5\iscc.exe" "MarkdownMonster.iss" 

"Signing the main EXE..."
& ".\signtool.exe" sign /v /n "West Wind Technologies" /sm  /tr "http://timestamp.digicert.com" /td SHA256 /fd SHA256 ".\Builds\CurrentRelease\MarkdownMonsterSetup.exe"

"Zipping up setup file..."
del ".\Builds\CurrentRelease\MarkdownMonsterSetup.zip"
.\7z a -tzip ".\Builds\CurrentRelease\MarkdownMonsterSetup.zip" ".\Builds\CurrentRelease\MarkdownMonsterSetup.exe"

# Portable build includes _IsPortable flag by default
out-file -FilePath .\Distribution\_IsPortable -InputObject "forces the settings to be read from .\PortableSettings rather than %appdata%"
"Zipping up portable setup file..."
del ".\Builds\CurrentRelease\MarkdownMonsterPortable.zip"
.\7z a -tzip -r ".\Builds\CurrentRelease\MarkdownMonsterPortable.zip" ".\Distribution\*.*" ".\Distribution\_IsPortable" ".\MarkdownMonsterPortable.md"

remove-item .\Distribution\_IsPortable

$version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo("$PSScriptRoot\builds\currentrelease\MarkdownMonsterSetup.exe").FileVersion
$version = $version.Trim()
"Initial Version: " + $version

# Remove last two .0 version tuples if it's 0
if($version.EndsWith(".0.0")) {
    $version = $version.SubString(0,$version.Length - 4);
}
else {
    if($version.EndsWith(".0")) {    
        $version = $version.SubString(0,$version.Length - 2);
    }
}
"Truncated Version: " + $version

"Writing Version File for: " + $version
$versionFilePath = ".\builds\currentrelease\MarkdownMonster_Version_Template.xml"
$versionFile = Get-Content -Path $versionFilePath  

$versionFile = $versionFile.Replace("{{version}}",$version).Replace("{{preview-version}}",$version).Replace("{{date}}",[System.DateTime]::Now.ToString("MMMM d, yyyy"))
$versionFile
""

out-file -filepath $versionFilePath.Replace("_Template","")  -inputobject $versionFile


get-childitem .\builds\CurrentRelease\* -include *.* | foreach-object { "{0}`t{1}`t{2:n0}`t`t{3}" -f $_.Name, $_.LastWriteTime, $_.Length, [System.Diagnostics.FileVersionInfo]::GetVersionInfo($_).FileVersion }
""
"Done..."