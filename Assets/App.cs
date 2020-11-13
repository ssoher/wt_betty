/*
War Thunder Betty
Apr 25 2020 - 16:02
Sarper Soher - https://www.accidentalee.com
*/

namespace WarThunderBetty {
    using UnityEngine;
    using UnityEngine.Networking;
    using System.Collections;
    using Newtonsoft.Json;

    [RequireComponent(typeof(AudioSource))]
    public sealed class App : MonoBehaviour {
        public string Ip;
        public float UpdateInterval;

        public float FuelRatio;
        public float AngleOfAttack;
        public float GLimit;
        public int ProximityHeight;
        public int GearsSpeed;
        public int MinimumSpeedForAoA;

        public AudioSource StallAudioSource, FuelAudioSource, ProximityAudioSource, OverGAudioSource, GearAudioSource;
        public UILabel ConnectButtonLabel;
        public UILabel ConnectionStatusLabel;
        public TweenColor TCBingoFuel, TCAoA, TCG, TCProximity, TCGear;

        private const string port = "8111";

        private float _lastUpdateTime;

        private State     _state;
        private Indicator _indicator;

        private IEnumerator _downloadCoroutine;

        private bool _isConnected;
        private bool _isConnecting;

        private void Awake() {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Application.targetFrameRate = 25;

            _state     = new State();
            _indicator = new Indicator();
        }

        private void Update() {
            if (_isConnected && Time.time > _lastUpdateTime + UpdateInterval) {
                DownloadServerData();

                _lastUpdateTime = Time.time;
            }
        }

        public void OnConnectButton() {
            if(_isConnected) {
                ConnectionStatusLabel.text = "Status: Disconnecting";
                Disconnect();
            }
            else {
                Connect();
                ConnectionStatusLabel.text = "Status: Connecting";
            }
        }

        public void OnBingoFuelUIChanged() {
            if (UIProgressBar.current == null) return;
            FuelRatio = UIProgressBar.current.value;
        }

        public void OnAoAUIChanged() {
            if (UIProgressBar.current == null) return;
            AngleOfAttack = Mathf.Lerp(0, 30, UIProgressBar.current.value);
        }

        public void OnAoAMinSpeedUIChanged() {
            if (UIProgressBar.current == null) return;
            MinimumSpeedForAoA = Mathf.RoundToInt(Mathf.Lerp(50, 750, UIProgressBar.current.value));
        }

        public void OnGForceUIChanged() {
            if (UIProgressBar.current == null) return;
            GLimit = Mathf.RoundToInt(Mathf.Lerp(5, 11, UIProgressBar.current.value));
        }

        public void OnProximityUIChanged() {
            if (UIProgressBar.current == null) return;
            ProximityHeight = Mathf.RoundToInt(Mathf.Lerp(200, 2000, UIProgressBar.current.value));
        }

        public void OnGearUIChanged() {
            if (UIProgressBar.current == null) return;
            GearsSpeed  = Mathf.RoundToInt(Mathf.Lerp(100, 500, UIProgressBar.current.value));
        }

        public void Connect() {
            if(!_isConnecting) StartCoroutine(ConnectCoroutine());
        }

        public void Disconnect() {
            _isConnected = false;
            ConnectionStatusLabel.text = "Status: Disconnected";
            ConnectButtonLabel.text = "Connect";

            ToggleTweenColor(TCBingoFuel, false);
            ToggleTweenColor(TCAoA, false);
            ToggleTweenColor(TCG, false);
            ToggleTweenColor(TCProximity, false);
            ToggleTweenColor(TCGear, false);

            ToggleAudio(FuelAudioSource, false);
            ToggleAudio(StallAudioSource, false);
            ToggleAudio(OverGAudioSource, false);
            ToggleAudio(ProximityAudioSource, false);
            ToggleAudio(GearAudioSource, false);
        }

        private IEnumerator ConnectCoroutine() {
            _isConnecting = true;

            var req = UnityWebRequest.Get($"http://{Ip}:{port}/state");
            yield return req.SendWebRequest();

            if (req.isNetworkError || req.isHttpError) {
                Debug.LogError(req.error);
                Disconnect();
                _isConnecting = false;
                ConnectionStatusLabel.text = $"Status: Failed to connect {req.error}";
                yield break;
            }

            _isConnecting = false;
            _isConnected = true;
            ConnectionStatusLabel.text = "Status: Connected";
            ConnectButtonLabel.text = "Disconnect";
        }

        private void DownloadServerData() {
            if (_downloadCoroutine != null) StopCoroutine(_downloadCoroutine);
            _downloadCoroutine = DownloadServerDataCoroutine();
            StartCoroutine(_downloadCoroutine);
        }

        private IEnumerator DownloadServerDataCoroutine() {
            // Download state json
            var req = UnityWebRequest.Get($"http://{Ip}:{port}/state");
            yield return req.SendWebRequest();

            if (req.isNetworkError || req.isHttpError) {
                Debug.LogError(req.error);
                Disconnect();
                yield break;
            }

            _state = JsonConvert.DeserializeObject<State>(req.downloadHandler.text);
            if (!_state.valid) yield break;

            if(_state.thrust1kgs < 50) yield break;

            // State is valid, download indicators json
            req = UnityWebRequest.Get($"http://{Ip}:{port}/indicators");
            yield return req.SendWebRequest();

            if (req.isNetworkError || req.isHttpError) {
                Debug.LogError(req.error);
                Disconnect();
                yield break;
            }

            _indicator = JsonConvert.DeserializeObject<Indicator>(req.downloadHandler.text);

            if (_indicator.valid && _indicator.type != "dummy_plane") {
                var ibf = IsBingoFuel();
                ToggleAudio(FuelAudioSource, ibf);
                ToggleTweenColor(TCBingoFuel, ibf);

                var isstall = IsStall();
                ToggleAudio(StallAudioSource, isstall);
                ToggleTweenColor(TCAoA, isstall);

                var isG = IsGOverlad();
                ToggleAudio(OverGAudioSource, isG);
                ToggleTweenColor(TCG, isG);

                var proximity = IsGroundProximity();
                ToggleAudio(ProximityAudioSource, proximity);
                ToggleTweenColor(TCProximity, proximity);

                var isgear = IsGearLimit();
                ToggleAudio(GearAudioSource, isgear);
                ToggleTweenColor(TCGear, isgear);
            }
        }

        private bool IsBingoFuel() {
            return (float)_state.Mfuel / _state.Mfuel0 < FuelRatio && _indicator.throttle > 0;
        }

        private bool IsStall() {
            return _state.AoA > AngleOfAttack && _state.IAS < MinimumSpeedForAoA;
        }

        private bool IsGOverlad() {
            return Mathf.Abs(_state.Ny) > GLimit;
        }

        private bool IsGroundProximity() {
            return _state.H < ProximityHeight && _state.Vy < 0 && (_state.gear == 0 || _state.flaps < 0);
        }

        private bool IsGearLimit() {
            return ((_state.gear > 0 && _state.IAS > GearsSpeed) ||
                    (_state.gear == 0 && _state.IAS < GearsSpeed && _indicator.altitudehour < 500 && _state.flaps > 20));
        }

        private void ToggleAudio(AudioSource aSource, bool play) {
            if(play && !aSource.isPlaying) aSource.Play();
            else aSource.Stop();
        }

        private void ToggleTweenColor(TweenColor tc, bool toggle) {
            if(toggle) tc.PlayForward();
            else {
                tc.Sample(0f, true);
                tc.enabled = false;
            }
        }
    }

    [System.Serializable]
    public sealed class State {
        [JsonProperty("valid")]                    public bool valid;
        [JsonProperty("aileron, %")]               public int aileron;
        [JsonProperty("elevator, %")]              public int elevator;
        [JsonProperty("rudder, %")]                public int rudder;
        [JsonProperty("flaps, %")]                 public int flaps;
        [JsonProperty("gear, %")]                  public int gear;
        [JsonProperty("H, m")]                     public int H;
        [JsonProperty("TAS, km/h")]                public int TAS;
        [JsonProperty("IAS, km/h")]                public int IAS;
        [JsonProperty("M")]                        public float M;
        [JsonProperty("AoA, deg")]                 public float AoA;
        [JsonProperty("AoS, deg")]                 public float AoS;
        [JsonProperty("Ny")]                       public float Ny;
        [JsonProperty("Vy, m/s")]                  public float Vy;
        [JsonProperty("Wx, deg/s")]                public int Wx;
        [JsonProperty("Mfuel, kg")]                public int Mfuel;
        [JsonProperty("Mfuel0, kg")]               public int Mfuel0;
        [JsonProperty("throttle 1, %")]            public int throttle1;
        [JsonProperty("mixture 1, %")]             public int mixture1;
        [JsonProperty("radiator 1, %")]            public int radiator1;
        [JsonProperty("magneto 1")]                public int magneto1;
        [JsonProperty("power 1, hp")]              public float power1hp;
        [JsonProperty("RPM 1")]                    public int RPM1;
        [JsonProperty("manifold pressure 1, atm")] public float manifoldpressure1;
        [JsonProperty("oil temp 1, C")]            public int oiltemp1C;
        [JsonProperty("pitch 1, deg")]             public float pitch1deg;
        [JsonProperty("thrust 1, kgs")]            public int thrust1kgs;
        [JsonProperty("efficiency 1, %")]          public int efficiency1;
        [JsonProperty("throttle 2, %")]            public int throttle2;
        [JsonProperty("mixture 2, %")]             public int mixture2;
        [JsonProperty("radiator 2, %")]            public int radiator2;
        [JsonProperty("magneto 2")]                public int magneto2;
        [JsonProperty("power 2, hp")]              public float power2hp;
        [JsonProperty("RPM 2")]                    public int rpm2;
        [JsonProperty("manifold pressure 2, atm")] public float manifoldpressure2;
        [JsonProperty("oil temp 2, C")]            public int oiltemp2c;
        [JsonProperty("pitch 2, deg")]             public float pitch2deg;
        [JsonProperty("thrust 2, kgs")]            public int thrust2;
        [JsonProperty("efficiency 2, %")]          public int efficiency2;

        public override string ToString() => $"{valid}\n{aileron}\n{elevator}\n{rudder}\n{flaps}\n{gear}\n{H}\n{TAS}\n{IAS}\n{M}\n{AoA}\n{AoS}\n{Ny}\n{Vy}\n{Wx}\n{Mfuel}\n{Mfuel0}\n{throttle1}\n{mixture1}\n{radiator1}\n{magneto1}\n{power1hp}\n{RPM1}\n{manifoldpressure1}\n{oiltemp1C}\n{pitch1deg}\n{thrust1kgs}\n{efficiency1}\n{throttle2}\n{mixture2}\n{rpm2}\n{manifoldpressure2}\n{oiltemp2c}\n{pitch2deg}\n{thrust2}\n{efficiency2}";
    }

    [System.Serializable]
    public sealed class Indicator {
        [JsonProperty("valid")]              public bool valid;
        [JsonProperty("type")]               public string type;
        [JsonProperty("speed")]              public float speed;
        [JsonProperty("speed_01")]           public float speed1;
        [JsonProperty("pedals")]             public float pedals;
        [JsonProperty("pedals1")]            public float pedals1;
        [JsonProperty("pedals2")]            public float pedals2;
        [JsonProperty("pedals3")]            public float pedals3;
        [JsonProperty("pedals4")]            public float pedals4;
        [JsonProperty("stick_elevator")]     public float stickelevator;
        [JsonProperty("stick_ailerons")]     public float stickailerons;
        [JsonProperty("vario")]              public float vario;
        [JsonProperty("altitude_hour")]      public float altitudehour;
        [JsonProperty("aviahorizon_roll")]   public float aviahorizonroll;
        [JsonProperty("aviahorizon_pitch")]  public float aviahorizonpitch;
        [JsonProperty("bank")]               public float bank;
        [JsonProperty("turn")]               public float turn;
        [JsonProperty("compass")]            public float compass;
        [JsonProperty("compass1")]           public float compass1;
        [JsonProperty("compass2")]           public float compass2;
        [JsonProperty("manifold_pressure")]  public float manifoldpressure;
        [JsonProperty("manifold_pressure1")] public float manifoldpressure1;
        [JsonProperty("rpm")]                public float rpm;
        [JsonProperty("rpm1")]               public float rpm1;
        [JsonProperty("oil_pressure")]       public float oilpressure;
        [JsonProperty("oil_pressure1")]      public float oilpressure1;
        [JsonProperty("oil_temperature")]    public float oiltemp;
        [JsonProperty("oil_temperature1")]   public float oiltemp1;
        [JsonProperty("mixture")]            public float mixture;
        [JsonProperty("mixture1")]           public float mixture1;
        [JsonProperty("mixture2")]           public float mixture2;
        [JsonProperty("mixture3")]           public float mixture3;
        [JsonProperty("mixture4")]           public float mixture4;
        [JsonProperty("mixture5")]           public float mixture5;
        [JsonProperty("fuel_pressure")]      public float fulepressure;
        [JsonProperty("fuel_pressure1")]     public float fulepressure1;
        [JsonProperty("gears_lamp")]         public float gearslamp;
        [JsonProperty("flaps")]              public float flaps;
        [JsonProperty("trimmer")]            public float trimmer;
        [JsonProperty("throttle")]           public float throttle;
        [JsonProperty("throttle1")]          public float throttle1;
        [JsonProperty("throttle2")]          public float throttle2;
        [JsonProperty("weapon1")]            public float weapon1;
        [JsonProperty("weapon2")]            public float weapon2;
        [JsonProperty("weapon3")]            public float weapon3;
        [JsonProperty("prop_pitch")]         public float proppitch;
        [JsonProperty("prop_pitch1")]        public float proppitc1;
        [JsonProperty("radiator_lever")]     public float radiatorlever;
        [JsonProperty("blister1")]           public float blister1;

        public override string ToString() => $"{valid}\n{type}\n{speed}\n{speed1}\n{pedals}\n{pedals1}\n{pedals2}\n{pedals3}\n{pedals4}\n{stickelevator}\n{stickailerons}\n{vario}\n{altitudehour}\n{aviahorizonroll}\n{aviahorizonpitch}\n{bank}\n{turn}\n{compass}\n{compass1}\n{compass2}\n{manifoldpressure}\n{manifoldpressure1}\n{rpm}\n{rpm1}\n{oilpressure}\n{oilpressure1}\n{oiltemp}\n{oiltemp1}\n{mixture}\n{mixture1}\n{mixture2}\n{mixture3}\n{mixture4}\n{mixture5}\n{fulepressure}\n{fulepressure1}\n{gearslamp}\n{flaps}\n{trimmer}\n{throttle}\n{throttle1}\n{throttle2}\n{weapon1}\n{weapon2}\n{weapon3}\n{proppitch}\n{proppitc1}\n{radiatorlever}\n{blister1}";
    }
}