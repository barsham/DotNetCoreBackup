SET CurPath=%cd%
ECHO %CurPath%
SCHTASKS /CREATE /SC DAILY /TN "DotNetCoreBackup" /TR %CurPath%"\run.bat" /ST 14:00