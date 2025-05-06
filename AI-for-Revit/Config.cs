using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace AI_for_Revit
{
    //internal class Config 
    //{
    //    internal string api_key {  get; set; }
    //    internal string provider {  get; set; }
    //    internal string model { get; set; }
    //}

    public class Config : INotifyPropertyChanged
    {
        private string _apiKey;
        private string _provider;
        private string _model;

        public string api_key
        {
            get => _apiKey;
            set
            {
                if (_apiKey != value)
                {
                    _apiKey = value;
                    OnPropertyChanged(nameof(api_key));
                }
            }
        }

        public string provider
        {
            get => _provider;
            set
            {
                if (_provider != value)
                {
                    _provider = value;
                    OnPropertyChanged(nameof(provider));
                }
            }
        }

        public string model
        {
            get => _model;
            set
            {
                if (_model != value)
                {
                    _model = value;
                    OnPropertyChanged(nameof(model));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class Configuration
    {
        private static readonly Lazy<Configuration> _instance =
            new Lazy<Configuration>(() => new Configuration());

        public static Configuration Instance => _instance.Value;

        public event EventHandler ConfigChanged;

        public Config Config { get; private set; }

        private Configuration()
        {
            LoadConfig();
        }

        private void LoadConfig()
        {
            string confjson = File.ReadAllText("config.json");
            Config = JsonConvert.DeserializeObject<Config>(confjson);

            if (Config == null)
            {
                Config = new Config();
            }
            else
            {
                Config.PropertyChanged += (s, e) => OnConfigChanged();
            }
        }

        public string GetApiKey() => Config.api_key;
        public string GetProvider() => Config.provider;
        public string GetModel() => Config.model;

        public void SaveNewConfig(string api_key, string provider, string model)
        {
            Config.api_key = api_key;
            Config.provider = provider;
            Config.model = model;

            string confjson = JsonConvert.SerializeObject(Config);
            File.WriteAllText("config.json", confjson);

            OnConfigChanged();
        }

        protected virtual void OnConfigChanged()
        {
            ConfigChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    //public class Configuration
    //{
    //    Config config { get; set; }
    //    private Configuration()
    //    {
    //        string confjson = File.ReadAllText("config.json");
    //        config = JsonConvert.DeserializeObject<Config>(confjson);
    //    }

    //    public string GetApiKey()
    //    {
    //        return config.api_key;
    //    }

    //    public string GetProvider() {
    //        return config.provider;
    //    }

    //    public string GetModel()
    //    {
    //        return config.model;
    //    }


    //    public void SaveNewConfig(string api_key, string provider, string model)
    //    {
    //        config.api_key = api_key;
    //        config.provider = provider;
    //        config.model = model;

    //        string confjson = JsonConvert.SerializeObject(config);
    //        File.WriteAllText("config.json", confjson);
    //    }

    //}
}
