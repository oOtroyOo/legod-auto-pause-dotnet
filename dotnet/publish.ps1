Remove-Item -Recurse ./publish -ErrorAction SilentlyContinue
dotnet clean
dotnet publish LeigodPause/LeigodPause.csproj -c Release -f net481 -p:PublishLayout=true -p:DisableServiceStop=true -o ./publish
dotnet restore