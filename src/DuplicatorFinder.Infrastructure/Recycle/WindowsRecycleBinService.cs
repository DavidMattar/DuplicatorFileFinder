using System.IO;
using DuplicatorFinder.Core.Abstractions;
using DuplicatorFinder.Core.Models;
using Microsoft.VisualBasic.FileIO;

namespace DuplicatorFinder.Infrastructure.Recycle;

/// <summary>
/// Implementação de <see cref="IRecycleBinService"/> baseada em
/// <see cref="Microsoft.VisualBasic.FileIO.FileSystem"/> — a forma oficial mais simples de
/// enviar arquivos para a Lixeira do Windows sem escrever interop COM manual.
/// Nunca exclui nada permanentemente: <see cref="RecycleOption.SendToRecycleBin"/> é sempre
/// o comportamento usado aqui, por design (exclusão permanente ficaria a cargo de uma tela
/// separada e explícita na UI, se algum dia for oferecida).
/// </summary>
public sealed class WindowsRecycleBinService : IRecycleBinService
{
    /// <inheritdoc />
    public Task<DeleteResult> SendToRecycleBinAsync(IEnumerable<string> paths)
    {
        // FileSystem.DeleteFile é uma chamada síncrona (e pode, dependendo do UIOption,
        // aguardar um diálogo do Windows); roda em uma thread de background para não
        // bloquear quem chamou (tipicamente a UI thread, via um RelayCommand assíncrono).
        return Task.Run(() =>
        {
            var succeeded = new List<string>();
            var failures = new List<(string Path, string Error)>();

            foreach (var path in paths)
            {
                try
                {
                    FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                    succeeded.Add(path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException or ArgumentException)
                {
                    // Continua tentando os demais arquivos mesmo se este falhar (ex: arquivo
                    // aberto em outro programa) — o chamador decide o que fazer com as falhas.
                    failures.Add((path, ex.Message));
                }
            }

            return new DeleteResult(succeeded, failures);
        });
    }
}
