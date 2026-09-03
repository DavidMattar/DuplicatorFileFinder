# Contexto de trabalho — DuplicatorFinder

Este arquivo não substitui `CLAUDE.md` (arquitetura e convenções do projeto) nem
`iaInstructions.md` (runbook de setup do zero) — leia os dois primeiro. Este aqui registra o
que foi construído e decidido em cada sessão de trabalho, para uma IA (ou pessoa) futura
não precisar reconstruir esse raciocínio do zero ao continuar o projeto. Está em ordem
cronológica: a **sessão 2** (correção do "Inverter seleção" e os modos de movimentação) está
no fim do arquivo e é a mais recente.

## Estado atual do projeto

- Branch `main`, sincronizado com `origin/main` (GitHub: `DavidMattar/DuplicatorFileFinder`).
- Último commit de código: `4557049` — "Fix invert-selection breaking the marked-file actions,
  add move modes" (sessão 2).
- Build limpo (0 erros/avisos), **27 testes automatizados passando** (17 em
  `DuplicatorFinder.Core.Tests`, 10 em `DuplicatorFinder.Infrastructure.Tests`).
- `.gitignore` ignora `bin/`, `obj/`, `.vs/`, `.idea/` e `/fileTesting/`.

---

# Sessão 1 — Preview, abrir locais e mover cópias

Fim desta sessão: commit `cddf516`, 26 testes passando. O `.gitignore` foi criado aqui (não
existia antes).

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
  movia as cópias, deixando o original no lugar).
  **Atualização (sessão seguinte)**: os dois comportamentos passaram a coexistir como
  `DuplicateMoveMode`, escolhido pelo usuário num diálogo ao clicar em "Mover selecionados" —
  ver "Modos de movimentação" na sessão 2, abaixo.
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

## Pendências levantadas na sessão 1 (ver a lista consolidada no fim do arquivo)

- Não existe projeto de testes para `DuplicatorFinder.App` (só Core e Infrastructure têm
  testes). A lógica de UI foi validada manualmente e via o harness descartável descrito acima.
- `DuplicateGroupViewModel.HasPreviewableImages` é calculado uma vez e não reage a mudanças na
  coleção `Files` depois de criado (ex: depois de excluir/mover arquivos do grupo, o botão
  "Preview" não desaparece mesmo que não sobre nenhuma imagem) — edge case raro, não corrigido.
- `ImageEntry`/`VideoEntry` em `Core/Models/` parecem não ser usados por nenhum detector (que
  usam records privados próprios, `DecodedImage`/`VideoMetadata`) — candidatos a código morto,
  não removidos ainda por não ter sido confirmado com o usuário.

---

# Sessão 2 — Correção do "Inverter seleção" e os dois modos de movimentação

Commit de código: `4557049`. Pedido do usuário, literalmente: *"na hora que eu clicar na opção
de excluir apos ser feita a varredura, se eu clico em inverter a seleção a exclusão dos marcados
não funciona, arrume isso"* + *"quando eu clicar em mover eu quero que o programa me dê mais uma
opção além da atual: pegar um de cada arquivo duplicado (o com a maior resolução) e o restante
das cópias movidas para uma única pasta dentro dessa selecionada"*.

Investigar esse relato levou a **dois** bugs distintos, os dois só reproduzíveis executando a
UI de verdade.

### Bug 1 — "Inverter seleção" tornava as ações inertes

`InvertSelection` invertia a marcação de todo arquivo, **inclusive do arquivo mantido**. O
resultado era um grupo com `IsKept == true` **e** `MarkedForDeletion == true` no mesmo arquivo
— combinação que o XML doc de `DuplicateFile.MarkedForDeletion` explicitamente proíbe. Como
`MoveSelectedAsync` filtrava por `IsMarkedForDeletion && !IsKept`, o único arquivo marcado de
cada grupo caía fora desse filtro e o botão respondia **"Nenhum arquivo selecionado para
mover"** com itens visivelmente marcados na tela. A exclusão "funcionava", mas apagava o
original e deixava o grupo sem nenhum mantido definido, o que quebrava tudo dali para frente.

**Correção**: a invariante do grupo passou a ter um guardião explícito,
`DuplicateGroupViewModel.NormalizeKeptFile()` / `PromoteToKeptFile()` — exatamente um mantido,
nunca marcado para exclusão. É chamado por `SelectRecommended`, `InvertSelection` e
`RemoveFilesFromGroups` (ações em massa e remoções), **não** a cada clique de checkbox: marcar
o original à mão continua sendo permitido, como sempre foi. Se todos os arquivos de um grupo
acabarem marcados, um deles é desmarcado para sobreviver — apagar um grupo inteiro é
justamente o que o app existe para evitar.

`FileCandidateViewModel.IsKept`/`Reason` deixaram de ser somente-leitura estáticos: agora
mudam via `SetKept(...)`, que notifica a UI (a coluna de motivo acompanha a troca de original).

### Bug 2 — miniatura que falha travava o arquivo pela sessão inteira

Achado enquanto se verificava a correção acima, num teste de movimentação em disco real.
`FilePathToThumbnailConverter` decodificava via `BitmapImage.UriSource`; quando a decodificação
**falha** (qualquer não-imagem — a lista de resultados mostra `<Image>` em toda linha,
independente do tipo — ou uma imagem corrompida), o `BitmapImage` **não fecha o arquivo que
abriu**. O arquivo ficava travado até o app fechar, e toda tentativa de excluí-lo ou movê-lo
falhava com "está sendo usado por outro processo", sem pista nenhuma na tela.

**Correção**: decodifica a partir de um `FileStream` próprio (`StreamSource` + `using`), que
fecha o handle mesmo se `EndInit()` lançar, e nem tenta decodificar o que não é imagem
(`FileTypeClassifier.IsImageExtension`). Verificado com uma sonda que gera um `.jpg` corrompido,
chama o conversor e tenta `File.Move` em seguida: falhava antes, passa agora.

Vale como aviso geral: **nenhum teste com mock pegaria nenhum dos dois bugs**. Os dois só
apareceram executando a UI de verdade (a técnica descrita em "Como testar a UI de verdade").

### Modos de movimentação (`DuplicateMoveMode`)

Clicar em "Mover selecionados" agora abre primeiro `Views/MoveModeDialog.xaml`
(`MoveModeViewModel`), com duas opções — e só depois pergunta a pasta de destino e confirma.
A estrutura criada no destino é **idêntica** nos dois modos (uma única `copias(x)`, com uma
subpasta `"{nome do sobrevivente} copies moved"` por grupo); o que muda é quem sobrevive e se
ele sai do lugar:

| | `MoveEntireGroup` (padrão, comportamento antigo) | `KeepHighestResolutionInPlace` (novo) |
|---|---|---|
| quem sobrevive | o arquivo mantido na tela (smart-select + ajuste manual) | o de **maior resolução** (largura × altura) do grupo |
| sobrevivente é movido? | sim, para a raiz de `copias(x)` | **não**, fica exatamente onde está |
| o que é movido | só os arquivos **marcados** | **todas** as outras cópias do grupo, marcadas ou não |

A diferença de escopo da última linha é deliberada e está escrita no diálogo antes de o usuário
confirmar: no modo por resolução a regra é "fica um de cada, o de maior resolução", então parar
em quem está marcado deixaria para trás justamente o arquivo que o smart-select desmarcou. Nos
dois modos a marcação continua decidindo **quais grupos** participam (grupo sem nada marcado é
ignorado). Se o usuário pedir para o modo novo também respeitar as caixas desmarcadas, o ajuste
é de uma linha em `ResultsViewModel.PlanMove`.

Detalhes de implementação:

- `IDuplicateMoveService.MoveGroupAsync` ganhou um parâmetro `bool moveKeptFile` — é a única
  coisa que a Infrastructure precisa saber sobre o modo escolhido (o enum inteiro não desce
  até lá). Coberto pelo teste `MoveGroupAsync_LeavesKeptFileInPlace_WhenMoveKeptFileIsFalse`.
- `ResultsViewModel.PlanMove` decide, por grupo, sobrevivente + arquivos a mover, **antes** de
  qualquer diálogo de confirmação — nada é mutado até o usuário confirmar. O plano é um
  `record` privado (`MoveGroupPlan`).
- No modo por resolução, depois da movimentação o sobrevivente é promovido a mantido
  (`PromoteToKeptFile`): ele continua na lista e, sem isso, ficaria marcado para exclusão com o
  papel de original em outro arquivo — um "Excluir selecionados" logo depois apagaria justamente
  a melhor versão.
- `MoveConfirmationViewModel` repete a explicação do modo via `MoveModeViewModel.Describe(...)`,
  em vez de manter uma segunda redação que pode divergir com o tempo.
- `RemoveFilesFromGroups` agora também remove o arquivo de `group.Model.Files`, senão
  `WastedBytes` (calculado no Core sobre essa lista) seguiria contando arquivos que já saíram
  do disco.

### Como os dois bugs foram reproduzidos (vale repetir a receita)

Ler o código não bastou: o caminho ViewModel → exclusão parecia correto, e um teste puro de
ViewModel passa. O que resolveu foi a técnica da seção "Como testar a UI de verdade" (sessão 1),
com dois detalhes novos que custaram tempo e valem ficar registrados:

- **`button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent))` NÃO executa o `Command` do
  botão** — o primeiro harness "reproduziu" um bug inexistente (nenhum botão fazia nada) só por
  causa disso. O que simula um clique de verdade é o peer de automação:
  `((IInvokeProvider)new ButtonAutomationPeer(button)).Invoke()`. Para `RadioButton`/`CheckBox`,
  `((IToggleProvider)new RadioButtonAutomationPeer(radio)).Toggle()`.
- **Arquivos de teste falsos escondem/criam problemas**: o harness criava `.jpg` com texto
  dentro, o que fez *todos* os arquivos ficarem travados pelo bug 2 e o move real falhar em
  100% dos casos. Gerar imagem de verdade (ou um PNG 1x1 em base64) é o que separa "o app está
  quebrado" de "o meu dado de teste está quebrado".

Foram 4 harnesses no scratchpad, cada um respondendo uma pergunta: (1) o VM inverte de fato?
(2) a virtualização/recycling da `ListBox` corrompe a marcação ao rolar? — não, 120 grupos
rolados nos dois sentidos, 0 divergência entre UI e VM; (3) os fluxos encadeados
(inverter → mover, inverter → excluir, excluir → inverter → excluir) mantêm a invariante do
grupo? (4) a movimentação em disco real produz a árvore de pastas certa nos dois modos, e a
janela nova renderiza? O harness 3 imprimia `!!! INVARIANTE QUEBRADA` sempre que um grupo
ficava com ≠1 mantido ou com um mantido marcado — é o que provou o bug 1 e depois a correção.

## Pendências consolidadas (não pedidas, só observações)

- **Não existe projeto de testes para `DuplicatorFinder.App`.** Isso ficou mais relevante depois
  desta sessão: a invariante "exatamente um mantido por grupo, nunca marcado" é exatamente o
  tipo de coisa que regride sem alarme, e hoje ela só está coberta pelos harnesses descartáveis
  (que não ficam no repo). Foi oferecido ao usuário e não pedido — criar o projeto muda o
  layout da solução, então ficou como decisão dele.
- **O cabeçalho de cada grupo ainda não reage a tudo.** `WastedBytes` passou a ser notificado
  (e `Model.Files` passou a ser podado junto com a lista da UI), mas
  `DuplicateGroupViewModel.HasPreviewableImages` continua calculado uma vez: depois de
  excluir/mover, o botão "Preview" não desaparece mesmo que não sobre nenhuma imagem no grupo.
- **`ImageEntry`/`VideoEntry` em `Core/Models/`** continuam sem uso por nenhum detector (que
  usam records privados próprios, `DecodedImage`/`VideoMetadata`) — candidatos a código morto,
  não removidos por falta de confirmação.
- **Modo por resolução move também as cópias desmarcadas** do grupo (decisão explicada acima).
  Se o usuário mudar de ideia, é uma linha em `ResultsViewModel.PlanMove`.
- **`FilePathToThumbnailConverter` continua decodificando de forma síncrona e sem cache**, agora
  com o atalho de nem tentar arquivos que não são imagem. O carregamento assíncrono com cache
  LRU segue como polish para uma versão futura.
