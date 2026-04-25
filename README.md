# TelegramLive

Aplicativo para Windows, desenvolvido em C# com WinForms, para retransmitir videos e lives do YouTube para um servidor RTMP do Telegram usando `yt-dlp` e `ffmpeg`.

## Requisitos

- Windows
- `ffmpeg.exe` em `TelegramLiveRelay/tools/`
- `yt-dlp.exe` em `TelegramLiveRelay/tools/`
- `deno.exe` em `TelegramLiveRelay/tools/` e opcional

## Como compilar

```powershell
dotnet publish .\TelegramLiveRelay\TelegramLiveRelay.csproj -c Release
```

Saida:

```text
TelegramLiveRelay\bin\Release\net8.0-windows\win-x64\publish\
```

Opcionalmente:

```powershell
.\build-framework.ps1
```

## Como usar

1. Abra `TelegramLiveRelay.exe`.
2. Informe o servidor RTMP do Telegram, a stream key e a URL do YouTube.
3. Se necessario, informe um arquivo `cookies.txt`.
4. Escolha a resolucao, o tamanho do video e a qualidade do audio.
5. Clique em `Iniciar`.
6. Para encerrar, clique em `Parar`.

## Observacao

O programa salva as configuracoes em `appsettings.txt`.

Participe do grupo no Telegram e faça o download do programa: https://t.me/OFICINADEBOTS

<img width="850" height="712" alt="image" src="https://github.com/user-attachments/assets/d418ad31-54da-4cfe-bf2c-e230ce461ace" />

