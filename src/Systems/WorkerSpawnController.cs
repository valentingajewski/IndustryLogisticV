using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Native;

namespace IndustryLogisticV.Systems
{
    public sealed class WorkerSpawnController
    {
        private readonly List<string> _workerModels;
        private readonly Dictionary<string, string> _workerNameOverrides;

        private int _workerIndex;

        public WorkerSpawnController(IEnumerable<string> workerModels)
        {
            _workerModels = workerModels == null
                ? new List<string>()
                : new List<string>(workerModels);
            _workerNameOverrides = CreateWorkerNameOverrides();
        }

        public string SelectedWorkerDisplayName
        {
            get { return GetWorkerDisplayName(SelectedModelName); }
        }

        private string SelectedModelName
        {
            get
            {
                if (_workerModels.Count == 0)
                {
                    return string.Empty;
                }

                return _workerModels[_workerIndex];
            }
        }

        public void ChangeSelection(int delta)
        {
            var count = _workerModels.Count;
            if (count == 0)
            {
                return;
            }

            _workerIndex = (_workerIndex + delta + count) % count;
        }

        public void ApplySelectedWorkerModel(Action<string> showStatus, Action<int> wait)
        {
            if (_workerModels.Count == 0)
            {
                showStatus("No worker models configured.");
                return;
            }

            var modelName = SelectedModelName;
            var model = new Model(modelName);
            if (!model.IsInCdImage || !model.IsValid || !model.Request(1000))
            {
                showStatus("Failed to request worker model.");
                return;
            }

            if (Game.Player.Character != null && Game.Player.Character.Exists() && Game.Player.Character.CurrentVehicle != null && Game.Player.Character.CurrentVehicle.Exists())
            {
                showStatus("Exit your vehicle before changing character model.");
                model.MarkAsNoLongerNeeded();
                return;
            }

            var changed = Game.Player.ChangeModel(model);
            if (!changed)
            {
                Function.Call(Hash.SET_PLAYER_MODEL, Game.Player.Handle, model.Hash);
                wait(0);
                var refreshed = Game.Player.Character;
                changed = refreshed != null && refreshed.Exists() && refreshed.Model.Hash == model.Hash;
            }

            if (changed)
            {
                var refreshed = Game.Player.Character;
                if (refreshed != null && refreshed.Exists())
                {
                    Function.Call(Hash.SET_PED_DEFAULT_COMPONENT_VARIATION, refreshed.Handle);
                }
            }

            model.MarkAsNoLongerNeeded();

            showStatus(changed
                ? string.Format("Worker switched to {0}", GetWorkerDisplayName(modelName))
                : "Model switch failed.");
        }

        private string GetWorkerDisplayName(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return "Unknown Worker";
            }

            string overrideName;
            if (_workerNameOverrides.TryGetValue(modelName, out overrideName))
            {
                return overrideName;
            }

            var cleaned = modelName.Replace("_", " ").Trim();
            if (cleaned.Length == 0)
            {
                return "Worker";
            }

            var words = cleaned.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (words.Count >= 3 && (words[0].Length <= 2 || words[0].Equals("mp", StringComparison.OrdinalIgnoreCase)))
            {
                words = words.Skip(2).ToList();
            }

            if (words.Count > 0)
            {
                int parsed;
                if (int.TryParse(words[words.Count - 1], out parsed))
                {
                    words.RemoveAt(words.Count - 1);
                }
            }

            if (words.Count == 0)
            {
                words.Add("Worker");
            }

            for (int i = 0; i < words.Count; i++)
            {
                words[i] = char.ToUpper(words[i][0]) + (words[i].Length > 1 ? words[i].Substring(1).ToLowerInvariant() : string.Empty);
            }

            return string.Join(" ", words);
        }

        private static Dictionary<string, string> CreateWorkerNameOverrides()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "s_m_m_dockwork_01", "Dock Worker" },
                { "s_m_y_construct_01", "Construction Worker" },
                { "s_m_m_trucker_01", "Long-Haul Trucker" },
                { "mp_m_freemode_01", "Freemode Male" },
                { "mp_f_freemode_01", "Freemode Female" },
            };
        }
    }
}