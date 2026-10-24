using System.Collections.Generic;

namespace Mmorpg.Core.Stats
{
    /// <summary>Conjunto de atributos (primários e derivados) indexados por id. Ausente = 0.</summary>
    public sealed class StatSheet
    {
        private readonly Dictionary<string, double> _values = new Dictionary<string, double>();

        public double Get(string id)
        {
            double v;
            return _values.TryGetValue(id, out v) ? v : 0.0;
        }

        public bool Has(string id) { return _values.ContainsKey(id); }

        public void Set(string id, double value) { _values[id] = value; }

        public void Add(string id, double delta)
        {
            double v;
            _values.TryGetValue(id, out v);
            _values[id] = v + delta;
        }

        public IEnumerable<KeyValuePair<string, double>> All { get { return _values; } }

        public StatSheet Clone()
        {
            var c = new StatSheet();
            foreach (var kv in _values) c._values[kv.Key] = kv.Value;
            return c;
        }
    }
}
