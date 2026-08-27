namespace DuplicatorFinder.Core.Detection.Support;

/// <summary>
/// Estrutura de dados clássica Union-Find (Disjoint Set), usada para agrupar itens que
/// foram comparados par a par em componentes conectados.
/// Necessária porque "similar" não é uma relação de equivalência estrita — A parecido com B
/// e B parecido com C não garante que A seja parecido com C — mas para os fins desta
/// ferramenta é razoável (e é o que o usuário espera) tratar toda essa cadeia de
/// similaridade como um único grupo de duplicados.
/// </summary>
public sealed class UnionFind
{
    private readonly int[] _parent;
    private readonly int[] _rank;

    public UnionFind(int size)
    {
        _parent = new int[size];
        _rank = new int[size];

        for (var i = 0; i < size; i++)
        {
            _parent[i] = i;
        }
    }

    /// <summary>Encontra o representante (raiz) do componente ao qual o item pertence, comprimindo o caminho percorrido para acelerar buscas futuras.</summary>
    public int Find(int item)
    {
        if (_parent[item] != item)
        {
            _parent[item] = Find(_parent[item]);
        }

        return _parent[item];
    }

    /// <summary>Une os componentes de dois itens (não faz nada se já estiverem no mesmo componente).</summary>
    public void Union(int itemA, int itemB)
    {
        var rootA = Find(itemA);
        var rootB = Find(itemB);

        if (rootA == rootB)
        {
            return;
        }

        // Union by rank: sempre pendura a árvore menor sob a maior, para manter Find() rápido.
        if (_rank[rootA] < _rank[rootB])
        {
            (rootA, rootB) = (rootB, rootA);
        }

        _parent[rootB] = rootA;

        if (_rank[rootA] == _rank[rootB])
        {
            _rank[rootA]++;
        }
    }

    /// <summary>Agrupa todos os índices 0..size-1 pelo componente ao qual pertencem.</summary>
    public IEnumerable<List<int>> GetComponents()
    {
        var componentsByRoot = new Dictionary<int, List<int>>();

        for (var i = 0; i < _parent.Length; i++)
        {
            var root = Find(i);
            if (!componentsByRoot.TryGetValue(root, out var members))
            {
                members = [];
                componentsByRoot[root] = members;
            }

            members.Add(i);
        }

        return componentsByRoot.Values;
    }
}
