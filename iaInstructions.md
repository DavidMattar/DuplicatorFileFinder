# Instruções para IA — Setup e Execução do DuplicatorFinder

Este arquivo existe para que uma IA (ou qualquer automação) consiga, numa máquina Windows
nova e sem nenhuma configuração prévia, colocar o app **DuplicatorFinder** rodando do zero.
Siga os passos na ordem. Cada passo tem um comando exato e um critério de sucesso — se o
critério não for atingido, vá para a seção **Troubleshooting** antes de tentar qualquer outra coisa.

Todos os comandos abaixo são **PowerShell**, executados a partir da raiz do projeto (a pasta
onde está este arquivo, `DuplicatorFinder.sln`, `src/` e `tests/`).

## Contexto do projeto

- App desktop **C# / .NET 8 / WPF**, clone do "Easy Duplicate Finder": encontra arquivos
  duplicados exatos, imagens e vídeos visualmente parecidos, e permite excluir as cópias
  (sempre para a Lixeira do Windows, nunca exclusão permanente).
- Só funciona em **Windows** (WPF é uma tecnologia exclusiva do Windows).
- Estrutura da solução: `src/DuplicatorFinder.Core` (lógica pura), `src/DuplicatorFinder.Infrastructure`
  (ffmpeg, Lixeira, configurações em disco), `src/DuplicatorFinder.App` (interface WPF),
  `tests/` (testes automatizados).

## Passo 1 — Verificar se o .NET 8 SDK está instalado

```powershell
dotnet --version
```

**Critério de sucesso:** imprime uma versão começando com `8.` (ex: `8.0.424`).

Se o comando **não for reconhecido** (`dotnet : The term 'dotnet' is not recognized...`), o
SDK não está instalado. Instale com:

```powershell
winget install --id Microsoft.DotNet.SDK.8 --source winget --accept-package-agreements --accept-source-agreements
```

Depois de instalar, **abra um terminal novo** (o `PATH` do processo atual não é atualizado
automaticamente). Se estiver rodando em um ambiente onde não é possível abrir um terminal
novo, adicione o caminho manualmente só nesta sessão:

```powershell
$env:PATH += ";C:\Program Files\dotnet"
```

Repita `dotnet --version` até o critério de sucesso ser atingido antes de seguir para o Passo 2.

## Passo 2 — Restaurar pacotes e compilar

```powershell
dotnet build DuplicatorFinder.sln
```

**Critério de sucesso:** a última linha impressa é `Build succeeded.` com `0 Error(s)`.

Este comando baixa automaticamente todos os pacotes NuGet necessários (internet é
necessária apenas nesta etapa, para os pacotes — não para rodar o app depois). A primeira
build pode levar 1–2 minutos; builds seguintes são bem mais rápidas.

Se aparecer qualquer erro, **não tente "corrigir" alterando versões de pacotes por conta
própria** — leia a mensagem de erro completa primeiro. Erros de rede (`Unable to load the
service index...`) geralmente significam que a máquina não tem acesso à internet ou está
atrás de um proxy/firewall corporativo bloqueando `api.nuget.org`.

## Passo 3 — Rodar os testes automatizados (recomendado, não obrigatório)

```powershell
dotnet test DuplicatorFinder.sln
```

**Critério de sucesso:** duas linhas `Passed!` (uma para `DuplicatorFinder.Core.Tests`, uma
para `DuplicatorFinder.Infrastructure.Tests`), com `Failed: 0` em ambas (total esperado: 21
testes). Isso confirma que a lógica de detecção de duplicados, o scanner de arquivos e a
persistência de configurações estão funcionando corretamente nesta máquina antes de abrir a UI.

## Passo 4 — Executar o app

```powershell
dotnet run --project src\DuplicatorFinder.App\DuplicatorFinder.App.csproj
```

**Critério de sucesso:** uma janela chamada "DuplicatorFinder" abre na tela.

Alternativa (depois de já ter compilado no Passo 2), executar o `.exe` gerado diretamente:

```powershell
Start-Process "src\DuplicatorFinder.App\bin\Debug\net8.0-windows\DuplicatorFinder.App.exe"
```

## O que acontece automaticamente ao usar o app (nenhuma ação manual necessária)

- **Detecção de imagens similares**: funciona imediatamente, sem nenhum download extra.
- **Detecção de vídeos similares**: é uma opção que o próprio usuário liga na tela inicial
  (checkbox desmarcado por padrão). Na primeira vez que for usada, o app baixa
  automaticamente os executáveis `ffmpeg.exe`/`ffprobe.exe` (~70MB) para
  `%LocalAppData%\DuplicatorFinder\ffmpeg\` — não é preciso instalar ffmpeg manualmente em
  lugar nenhum. Isso exige que a máquina tenha acesso à internet nesse momento.
- **Configurações do usuário** (pastas, sensibilidade, estratégia de manter) são salvas
  automaticamente em `%LocalAppData%\DuplicatorFinder\settings.json` a cada escaneamento
  iniciado, e recarregadas na próxima abertura do app.
- **Exclusão de arquivos** sempre vai para a Lixeira do Windows — nunca é permanente.

## Troubleshooting

| Sintoma | Causa provável | O que fazer |
|---|---|---|
| `dotnet` não é reconhecido mesmo após instalar o SDK | PATH não atualizado no processo atual | Abrir um terminal novo, ou `$env:PATH += ";C:\Program Files\dotnet"` |
| `dotnet build` falha com erro de rede/NuGet | Sem acesso à internet ou bloqueio de proxy/firewall | Verificar conectividade com `api.nuget.org`; não alterar versões de pacote para "contornar" isso |
| App fecha instantaneamente ao abrir (sem erro visível) | Raro; geralmente falha de inicialização de DI ou XAML | Rodar via `dotnet run` (não o `.exe` direto) para ver a exceção completa no console |
| Busca por vídeos não encontra nada / trava na primeira vez | Download do ffmpeg em andamento ou falhou por falta de rede | Aguardar (download pode levar de alguns segundos a 1–2 minutos); verificar internet |
| `System.IO.FileNotFoundException: ... System.Text.Json ...` ao escanear vídeos | Referência de pacote foi removida acidentalmente do `DuplicatorFinder.Infrastructure.csproj` | Confirmar que existe `<PackageReference Include="System.Text.Json" Version="9.0.0" />` nesse arquivo; se não existir, `dotnet add src\DuplicatorFinder.Infrastructure\DuplicatorFinder.Infrastructure.csproj package System.Text.Json --version 9.0.0` |
| Aviso de licença do SixLabors.ImageSharp durante o build | O pacote foi atualizado para a versão 3.x/4.x por engano | O projeto deve usar `SixLabors.ImageSharp` na versão `2.1.x` (ver `src\DuplicatorFinder.Core\DuplicatorFinder.Core.csproj`) — não atualizar para versões maiores sem também validar a compatibilidade com `CoenM.ImageSharp.ImageHash` |

## Não fazer

- Não rodar `dotnet build`/`dotnet run` fora da raiz do projeto sem apontar explicitamente
  para `DuplicatorFinder.sln` ou o `.csproj` do projeto App.
- Não apagar as pastas `bin/`/`obj/` "para resolver problemas" antes de tentar `dotnet build`
  normalmente primeiro — raramente é necessário e mascara a causa real de um erro.
- Não modificar as versões de `SixLabors.ImageSharp` (deve ficar em `2.1.x`) ou remover a
  referência a `System.Text.Json` do projeto Infrastructure — ambos foram fixados
  deliberadamente para evitar incompatibilidades reais já encontradas durante o
  desenvolvimento (ver comentários nos respectivos `.csproj`).
