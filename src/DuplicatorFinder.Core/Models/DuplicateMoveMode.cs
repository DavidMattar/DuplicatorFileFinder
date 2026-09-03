namespace DuplicatorFinder.Core.Models;

/// <summary>
/// Escolhe o que a ação "Mover selecionados" da tela de resultados faz com o arquivo que
/// sobrevive em cada grupo. As duas opções produzem exatamente a mesma estrutura de pastas no
/// destino (uma única pasta numerada "copias(x)", com uma subpasta "{nome} copies moved" por
/// grupo) — o que muda é só se o arquivo escolhido para sobreviver sai ou não do lugar onde
/// está hoje, e como ele é escolhido.
/// </summary>
public enum DuplicateMoveMode
{
    /// <summary>
    /// Move o grupo inteiro: o arquivo mantido (o marcado como original na tela, respeitando
    /// qualquer ajuste manual do usuário) vai para dentro da pasta numerada, e suas cópias
    /// para a subpasta ao lado. Depois da operação nada do grupo continua na pasta de origem.
    /// </summary>
    MoveEntireGroup,

    /// <summary>
    /// Mantém no lugar, de cada grupo, o arquivo de maior resolução (largura × altura) — ele
    /// não é movido — e move todas as outras cópias selecionadas para a subpasta daquele grupo
    /// dentro da pasta numerada. É o modo para "quero ficar com a melhor versão de cada imagem
    /// onde ela já está e tirar o resto da frente sem excluir".
    /// </summary>
    KeepHighestResolutionInPlace,
}
