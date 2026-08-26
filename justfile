_default:
    @just --list

generate-script:
    dotnet build SFD.Scripting.MapTranslationProject.csproj -t:GenerateScript

generate-translations:
    dotnet run --project Generator generate

validate-translations:
    dotnet run --project Generator validate
