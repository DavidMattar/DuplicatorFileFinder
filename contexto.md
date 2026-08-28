# Contexto de trabalho — DuplicatorFinder

Este arquivo não substitui `CLAUDE.md` (arquitetura e convenções do projeto) nem
`iaInstructions.md` (runbook de setup do zero) — leia os dois primeiro. Este aqui registra o
que foi construído e decidido numa sessão de trabalho específica, para uma IA (ou pessoa) futura
não precisar reconstruir esse raciocínio do zero ao continuar o projeto.

## Estado no fim desta sessão

- Branch `main`, sincronizado com `origin/main` (GitHub: `DavidMattar/DuplicatorFileFinder`).
- Último commit: `cddf516` — "Add preview, open-locations, and move-to-folder features on
  results screen".
- Build limpo (0 erros/avisos), 26 testes automatizados passando (17 em
  `DuplicatorFinder.Core.Tests`, 9 em `DuplicatorFinder.Infrastructure.Tests`).
- `.gitignore` criado nesta sessão (não existia antes): ignora `bin/`, `obj/`, `.vs/`, `.idea/`
  e `/fileTesting/`.

## Funcionalidades adicionadas nesta sessão (tela de Resultados)

Todas vivem em `src/DuplicatorFinder.App`, ligadas a cada `DuplicateGroupViewModel` (Preview,
Abrir locais) ou ao `ResultsViewModel` (Mover selecionados).

### 1. Preview lado a lado

- Botão **"Preview"** no cabeçalho de cada grupo, visível só quando
  `DuplicateGroupViewModel.HasPreviewableImages` é verdadeiro (pelo menos um arquivo do grupo é
  imagem, via `FileCandidateViewModel.IsImage`).
- Abre `Views/PreviewWindow.xaml` (não-modal — `Window.Show()`, não `ShowDialog()`) com as
  imagens do grupo em cards de 320px lado a lado, roláveis horizontalmente.
- Reaproveita `Converters/FilePathToThumbnailConverter`, que agora aceita um
  `ConverterParameter` opcional com a largura de decodificação (antes fixo em 48px para as
  miniaturas da lista).

### 2. Abrir locais

- Botão **"Abrir locais"**, sempre visível no cabeçalho do grupo.
- Abre `Views/OpenLocationsWindow.xaml` com uma aba (`TabControl`) por arquivo do grupo
  (`ViewModels/OpenLocationsViewModel` + `ViewModels/LocationTabViewModel`).
- Cada aba, ao ser selecionada, dispara automaticamente uma janela do Explorer do Windows já
  com uma busca feita pelo nome daquele arquivo, na pasta onde ele está — via o esquema de URI
  `search-ms:query=<nome>&crumb=location:<pasta>` passado para `explorer.exe`
  (`Process.Start`, `UseShellExecute = true`). Não precisa de automação COM.

### 3. Mover selecionados (em vez de excluir)

Comportamento **evoluiu duas vezes** nesta sessão — o que está descrito abaixo é a versão
final, atualmente no código:

- Botão **"Mover selecionados (escolher pasta de destino...)"** ao lado de "Excluir
  selecionados", em `ResultsView.xaml` → `ResultsViewModel.MoveSelectedAsync`.
- Fluxo: (1) popup nativo de pasta (`IDialogService.PickFolder`, pré-preenchido com
  `AppSettings.LastCopiesMoveDestinationFolder`) pergunta onde criar a estrutura; (2) diálogo de
  confirmação (`MoveConfirmationDialog`) mostra quantidade/tamanho/destino; (3) cria uma
  subpasta numerada `copias(x)` dentro do destino escolhido (`IDuplicateMoveService.CreateBatchFolder`
  — x é o menor inteiro livre, tipo "New folder (2)" do Windows); (4) para cada grupo
  selecionado, **move o arquivo mantido direto para dentro de `copias(x)`** e suas cópias para
  uma subpasta ao lado, `"{nome do mantido} copies moved"` (`IDuplicateMoveService.MoveGroupAsync`).
- **Importante**: diferente da exclusão, aqui o arquivo **mantido também é movido** (não fica
  mais "onde estava") — foi um pedido explícito do usuário depois da primeira versão (que só
  movia as cópias, deixando o original no lugar). Se pedirem para mudar esse comportamento de
  novo, é só remover o passo que move `keptFilePath` em `DuplicateMoveService.MoveGroupAsync`.
- Arquitetura: interface `Core/Abstractions/IDuplicateMoveService.cs`, implementação
  `Infrastructure/Move/DuplicateMoveService.cs` (usa `System.IO.Abstractions.IFileSystem`, não
  API do Windows — por isso fica testável com `MockFileSystem`, ver
  `tests/DuplicatorFinder.Infrastructure.Tests/Move/DuplicateMoveServiceTests.cs`). Registrado
  no DI em `App.xaml.cs`, injetado em `MainViewModel` → `ResultsViewModel`.
- Nunca sobrescreve nada: colisão de nome de arquivo ganha sufixo `" (1)"`, `" (2)"` etc.
  Falha em um arquivo não aborta os demais (mesmo padrão de tolerância a falha do
  `WindowsRecycleBinService`).

## Bug real encontrado e corrigido (não relacionado a nenhum pedido específico)

`Converters/FilePathToThumbnailConverter.cs` construía o `BitmapImage` via inicializador de
objeto (`new BitmapImage { CacheOption = ..., UriSource = ... }`) **sem chamar
`BeginInit()`/`EndInit()`**. Isso deixa o `BitmapImage` num estado "nunca finalizado": não
lança erro em `Convert()` nem no binding, só falha silenciosamente ao ser efetivamente
desenhado — ou seja, **as miniaturas da tela de Resultados provavelmente nunca apareceram**,
mesmo antes desta sessão, e nenhum teste automatizado cobria isso (só se percebe olhando a
tela renderizada de verdade). Corrigido com `BeginInit`/`EndInit` explícitos. Verificado via um
harness WPF descartável que renderizava a janela de verdade e inspecionava os pixels do
`BitmapImage` resultante (ver seção "Como testar a UI de verdade" abaixo).

## Pasta `fileTesting/` (dados de teste, não versionada)

- Está no `.gitignore` — **não existe** num clone novo do repositório.
- Contém uma estrutura gerada para exercitar o app manualmente: duplicados exatos em
  profundidades diferentes (`ExactDuplicates/`, `Nested/LevelA/LevelB/LevelC/`), um grupo grande
  numa pasta só (`ManyCopiesFlat/`, 8 cópias), variantes visualmente similares mas não exatas
  (`SimilarImages/` — redimensionadas 75%/50%, recomprimidas em qualidade 30/10) e distratores
  que não deveriam aparecer em nenhum grupo (`UniqueFiles/`).
- **Regenerar do zero** (a imagem-base é sintética — um gradiente gerado via
  `System.Drawing`, não uma foto real; qualquer imagem serve, o importante é a estrutura):

```powershell
Add-Type -AssemblyName System.Drawing
$root = "X:\DuplicatorFileFinder\fileTesting"

$baseBmp = New-Object System.Drawing.Bitmap 1600, 1200
$g = [System.Drawing.Graphics]::FromImage($baseBmp)
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.Point(0,0)), (New-Object System.Drawing.Point(1600,1200)),
    [System.Drawing.Color]::FromArgb(255,30,60,180), [System.Drawing.Color]::FromArgb(255,250,200,60))
$g.FillRectangle($brush, 0, 0, 1600, 1200)
$g.Dispose()
$srcPath = Join-Path $root "Image.jpg"
$baseBmp.Save($srcPath, [System.Drawing.Imaging.ImageFormat]::Jpeg)
$baseBmp.Dispose()

New-Item -ItemType Directory -Force -Path (Join-Path $root "ExactDuplicates") | Out-Null
1..3 | % { Copy-Item $srcPath (Join-Path $root "ExactDuplicates\copy_of_Image_$_.jpg") -Force }

$levelC = Join-Path $root "Nested\LevelA\LevelB\LevelC"
New-Item -ItemType Directory -Force -Path $levelC | Out-Null
Copy-Item $srcPath (Join-Path $root "Nested\LevelA\copy_of_Image_4.jpg") -Force
Copy-Item $srcPath (Join-Path $root "Nested\LevelA\LevelB\copy_of_Image_5.jpg") -Force
Copy-Item $srcPath (Join-Path $levelC "copy_of_Image_6.jpg") -Force

New-Item -ItemType Directory -Force -Path (Join-Path $root "ManyCopiesFlat") | Out-Null
1..8 | % { Copy-Item $srcPath (Join-Path $root ("ManyCopiesFlat\dup_{0:D2}.jpg" -f $_)) -Force }
```

(a parte de `SimilarImages/` e `UniqueFiles/` usa `Save-ResizedJpeg`/`Bitmap` — ver histórico do
chat desta sessão para o script completo, ou simplesmente pedir para a IA recriar: "popule a
pasta fileTesting de novo").

## Decisões que valem saber antes de tocar nesse código de novo

- **`FileTypeClassifier` (`Core/Support/FileTypeClassifier.cs`)** foi extraído do
  `DuplicateScanEngine` porque a UI (`FileCandidateViewModel.IsImage`) também precisava saber
  se um arquivo é imagem — antes a lista de extensões só existia, duplicada, dentro do engine.
- **Por que `IDuplicateMoveService` vive em `Infrastructure`, não em `Core`**: mover
  arquivo/criar pasta não depende de nenhuma API do Windows, mas segue o mesmo padrão do
  `IRecycleBinService`/`WindowsRecycleBinService` (contrato em `Core.Abstractions`,
  implementação concreta em `Infrastructure`) por consistência com o resto do projeto — ação
  que grava no disco fora do pipeline de detecção fica em Infrastructure.
- **Janelas de Preview e Abrir locais são não-modais** (`.Show()`, não `.ShowDialog()`) —
  de propósito, para o usuário poder continuar ajustando a seleção de arquivos na tela de
  Resultados enquanto olha o preview ou os Explorers abertos.
- **`AppSettings.LastCopiesMoveDestinationFolder`** é salvo a cada uso do "Mover
  selecionados" (não só ao trocar de tela), para pré-popular o popup na próxima vez — mesmo
  espírito do resto do `AppSettings` (ver `ScanSetupViewModel.SaveCurrentSettings`).
- **`AppSettings` é uma classe comum (`sealed class`), não um `record`** — não dá para usar
  `with { ... }` para "atualizar um campo". Ao salvar uma mudança pontual (ex:
  `LastCopiesMoveDestinationFolder`), é preciso reconstruir o objeto inteiro copiando os outros
  campos manualmente (ver `ResultsViewModel.MoveSelectedAsync`).

## Como testar a UI de verdade (sem clicar manualmente)

Diálogos nativos (`OpenFolderDialog`) são difíceis de automatizar; para validar que uma janela
WPF nova (`PreviewWindow`, `OpenLocationsWindow`, etc.) realmente renderiza o conteúdo certo,
funcionou bem: criar um projeto C#/WPF **descartável** fora do repo (no scratchpad), com
`ProjectReference` para `DuplicatorFinder.App`, que:

1. Cria uma `System.Windows.Application` manual (sem `App.xaml`), mesclando
   `Resources/Styles.xaml` via pack URI (`pack://application:,,,/DuplicatorFinder.App;component/Resources/Styles.xaml`)
   — sem isso, `StaticResource` usados nos XAML do app real (conversores, etc.) não resolvem.
2. Monta dados falsos (`DuplicateGroup`/`FileEntry` de mentira, imagens geradas com
   `SixLabors.ImageSharp` — já vem transitivamente via `Core`).
3. Chama o `DialogService` real (`ShowPreview`, `OpenLocations`) e usa o truque
   `DispatcherFrame`/`ContentRendered` para esperar a janela renderizar de verdade.
4. Tira um "screenshot" com `RenderTargetBitmap.Render(window)` + `PngBitmapEncoder` — sem
   precisar de captura de tela real nem de nenhuma lib de automação de UI.

Foi assim que o bug do `BitmapImage` (seção acima) foi encontrado: a imagem aparecia em branco
no screenshot mesmo sem nenhum erro lançado em lugar nenhum.

## Pendências / coisas a considerar no futuro (não pedidas ainda, só observações)

- Não existe projeto de testes para `DuplicatorFinder.App` (só Core e Infrastructure têm
  testes). A lógica de UI foi validada manualmente e via o harness descartável descrito acima.
- `DuplicateGroupViewModel.HasPreviewableImages` é calculado uma vez e não reage a mudanças na
  coleção `Files` depois de criado (ex: depois de excluir/mover arquivos do grupo, o botão
  "Preview" não desaparece mesmo que não sobre nenhuma imagem) — edge case raro, não corrigido.
- `ImageEntry`/`VideoEntry` em `Core/Models/` parecem não ser usados por nenhum detector (que
  usam records privados próprios, `DecodedImage`/`VideoMetadata`) — candidatos a código morto,
  não removidos ainda por não ter sido confirmado com o usuário.
