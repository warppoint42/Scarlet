using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class HeartRatePluginExampleTriangle : MonoBehaviour {
	private MenuState		m_state	= MenuState.START;
	private List<BleDevice>	m_adresses = new List<BleDevice>();
	public HeartRate		m_heartrate;
	private string			s_LabelText;
	private bool			b_Loggin = true;
	private string			m_status;
	public GUISkin			m_skin;
	private Vector2			m_scrollDistance;
	private BleDevice		m_currentDevice;
//	private	bool			b_ControlPointFeatured = false;
	private uint			m_currentrate;
	private float			m_tickrate = 1;
	private bool			m_shouldtick;
	private LineRenderer	lr;
	private List<Vector3>	i_DotArray = new List<Vector3>();
	private int				m_lastused;
 	
	void Start () {
		lr = GetComponent<LineRenderer>();
		for (int i = 0; i < 500; i++)
			i_DotArray.Add(new Vector3(i*.1f,0,0));
		for (int j = 0; j < i_DotArray.Count; j++) {
			lr.SetPosition(j,i_DotArray[j]);	
		}
		BluetoothLEPlugin.HRControlPointFeatureEvent += ControlPointFeatured;
		if (b_Loggin)
			Debug.Log("HeartRateScene: HRControlPointFeatureEvent subscribed");
		BluetoothLEPlugin.BleDeviceScanEvent += OnDeviceScan;
		if (b_Loggin)
			Debug.Log("HeartRateScene: BleDeviceScanEvent subscribed");
		BluetoothLEPlugin.BleDeviceDisconnectEvent += OnDeviceDisconnected;
		if (b_Loggin)
			Debug.Log("HeartRateScene: BleDeviceDisconnectEvent subscribed");
		m_status = "please hit update";
	}

	void OnGUI() {
		GUI.skin = m_skin;
		GUIStyle labelStyle =  GUI.skin.GetStyle("Label_left");
		#if UNITY_ANDROID
			labelStyle.fontSize  = 31;//set in Asset->Resources->Menu: in Inspector: Button->Overflow->Font Size; same for Label
		#elif UNITY_IPHONE && !UNITY_EDITOR	
			labelStyle.fontSize  = 25;
		#elif UNITY_STANDALONE_OSX || UNITY_EDITOR
			labelStyle.fontSize  = 15;
		#endif
		//Height of Label on Bottom
		float labelHeight = m_skin.GetStyle("Label").CalcHeight(new GUIContent(m_status),Screen.width-20);
		GUI.Label(new Rect(10, Screen.height - labelHeight,Screen.width - 20, labelHeight),m_status);
		if (m_state == MenuState.START) {
			Vector2 calcSize = m_skin.GetStyle("Button").CalcSize(new GUIContent("Update"));
			if (GUI.Button(new Rect(Screen.width / 2 - calcSize.x / 2 - 10,10,calcSize.x+20,calcSize.y+20),"Update")) {
				m_state = MenuState.SCANNING;
				m_currentDevice = null;
				UpdateButtonAction();
			}
		} else if (m_state == MenuState.SCANNING) {
			//Width(.x) and Height(.y) of "Stop Scan"-Button:
			Vector2 calcSize = m_skin.GetStyle("Button").CalcSize(new GUIContent("Update"));
			if (GUI.Button(new Rect(Screen.width / 2 - calcSize.x / 2 - 10,10,calcSize.x+20,calcSize.y+20),"Update")) {
				m_currentDevice = null;
				UpdateButtonAction();
			}
			m_scrollDistance = GUI.BeginScrollView(new Rect(10,10+calcSize.y+20+50,Screen.width-20, Screen.height - (10+calcSize.y+20+50+labelHeight+50)),m_scrollDistance,new Rect(0,0,Screen.width-20, m_adresses.Count * (70+calcSize.y)));
			//Width of the Scrollview: Screen.width-20
			//Height of the Scrollview:  Screen.height - (10+calcSize.y+20+50+labelHeight+50)
			float f_ScrollFill = 0;
			if ((m_adresses.Count * (70+calcSize.y)) >= (Screen.height - (10+calcSize.y+20+50+labelHeight+50)))
				f_ScrollFill = 110;
			int index = 0;
			//Width(.x) and Height(.y) of "Connect"-Button:
			calcSize = m_skin.GetStyle("Button").CalcSize(new GUIContent("Connect"));
			foreach (BleDevice kv in m_adresses) {
				//Height of Label with Devicenames
				labelHeight = m_skin.GetStyle("Label").CalcHeight(new GUIContent("DDD"),300);
				GUI.Label(new Rect(0,(calcSize.y+20-labelHeight)/2 + index*(calcSize.y+20+50),Screen.width-(20+f_ScrollFill) - calcSize.x-70,labelHeight),kv.Name,m_skin.FindStyle("Label_left"));
				if (GUI.Button(new Rect(Screen.width-(20+f_ScrollFill) - (calcSize.x+20),index*(calcSize.y+20+50),calcSize.x+20,calcSize.y+20),"Connect")) {
					m_currentDevice = kv;
					m_state = MenuState.START;
				}
				index++;
			}
			GUI.EndScrollView();
			if (m_currentDevice != null)
				ConnectDevice(m_currentDevice);
			//} else if (m_state == MenuState.BINDING) {
		} else if (m_state == MenuState.CONNECTED) {
			Vector2 calcSize = m_skin.GetStyle("Button").CalcSize(new GUIContent("Disconnect"));//org update instead of Disconnect
			if (GUI.Button(new Rect(Screen.width / 2 - calcSize.x / 2 - 10,10,calcSize.x+20,calcSize.y+20),"Disconnect")) {//org update instead of Disconnect
				m_currentDevice = null;
				s_LabelText = "";
				clearMAC();
				if (m_heartrate != null){
					m_heartrate.Reset();//unsubscribing BleDeviceEvents
					m_status = "disconnecting HeartRate";
					m_heartrate.Disconnect();
				} else {
					OnDeviceDisconnected(8,null);
				}
			}
			float tmpHeight = labelHeight;
			//Height of whole content:
			labelHeight = m_skin.GetStyle("Label_left").CalcHeight(new GUIContent(s_LabelText),Screen.width-20);
			m_scrollDistance = GUI.BeginScrollView(new Rect(10,10+calcSize.y+20+50,Screen.width - 20, Screen.height - (10+calcSize.y+20+50+tmpHeight+50)),m_scrollDistance,new Rect(0,0,Screen.width - 20,labelHeight));
			GUI.Label(new Rect(0,0,Screen.width-20,labelHeight),s_LabelText,m_skin.GetStyle("Label_left"));
			GUI.EndScrollView();
		}
	}
	
	public void UpdateButtonAction() {
		if (b_Loggin)
			Debug.Log("HeartRateScene: Update-Button hit");
		BluetoothLEPlugin.Initialize(b_Loggin);
		m_status = "checking Bluetooth low energy";
		#if UNITY_ANDROID
			StartCoroutine(StartDelayedScan(0));
		#elif UNITY_IPHONE && !UNITY_EDITOR	
			StartCoroutine(StartDelayedScan(0.2f));
		#elif UNITY_STANDALONE_OSX || UNITY_EDITOR
			StartCoroutine(StartDelayedScan(0.2f));
		#endif
	}
	
	private IEnumerator StartDelayedScan(float seconds) {
		yield return new WaitForSeconds(seconds);
		try {
			m_status = "searching for Devices...";
			if (b_Loggin)
				Debug.Log("HeartRateScene: calling ScanForDevices");
			BluetoothLEPlugin.BleDeviceFoundEvent -= OnDeviceFound;
			BluetoothLEPlugin.BleDeviceFoundEvent += OnDeviceFound;
			if (b_Loggin)
				Debug.Log("HeartRateScene: BleDeviceFoundEvent un-/resubscribed");
			BluetoothLEPlugin.Scan(90);
		} catch (BluetoothLEPluginException p) {
			Debug.Log(p.Message);
			if (b_Loggin)
				Debug.Log("HeartRateScene: got Exception, checking Ble status");
			m_status = "Bluetooth low energy error checking status...";
			BluetoothLEPlugin.CheckBLEStatus();
		}
	}
	
	private void clearMAC(){
		if (m_adresses != null)
			m_adresses.Clear();
	}
	
	private void OnDeviceScan(int reason, bool StartScanAgain) {
		if (b_Loggin)
			Debug.Log("HeartRateScene: got OnDeviceScan-Event with reason: " + (scanReason)reason);
		if (reason == 4){//Timeout
			m_status = "Scan aborted, reason: " +(scanReason)reason + "\nplease check and hit update";
			if (m_adresses != null)
				m_adresses.Clear();
		} else if (reason == 0){//Ble OK
			m_status = "Scan possible, scanning...";
			if (StartScanAgain){
				#if UNITY_ANDROID
				StartCoroutine(StartDelayedScan(0));
				#elif UNITY_IPHONE && !UNITY_EDITOR	
				StartCoroutine(StartDelayedScan(0.2f));
				#elif UNITY_STANDALONE_OSX || UNITY_EDITOR
				StartCoroutine(StartDelayedScan(0.2f));
				#endif
			}
		} else {
			m_status = "Scan impossible, reason: " + (scanReason)reason + "\nPlease check and hit scan again";
			if (reason == 3){
				if (b_Loggin)
					Debug.Log("HeartRateScene: OnDeviceScan with false, reason 3 (powered off)\ncalling EnableBluetooth");
				BluetoothLEPlugin.EnableBluetooth();
			}
		}
	}
	
	private void OnDeviceFound(BleDevice device) {
		if (b_Loggin)
			Debug.Log("HeartRateScene: DevicefoundEvent received, adding Adress");
		m_status = "Found Device(s)\nplease hit connect";
		m_adresses.Add(device);
	}
	
	private void OnDeviceConnected(BleDevice device) {
		BluetoothLEPlugin.BleReadyForSyncEvent += OnReadyForSync;
		if (b_Loggin)
			Debug.Log("HeartRateScene: Device successfully connected and ReadyForSyncEvent subscribed");
	}
	
	private void OnReadyForSync(BleDevice device) {
		m_state = MenuState.CONNECTED;
		BluetoothLEPlugin.BleDeviceConnectEvent -= OnDeviceConnected;
		if (b_Loggin)
			Debug.Log("HeartRateScene: BleDeviceConnectEvent unsubscribed");
		HeartRate.OnHRMDataReceived -= On_DataReceived;
		HeartRate.OnHRMDataReceived += On_DataReceived;
		if (b_Loggin)
			Debug.Log("HeartRateScene: OnHRMDataReceived un-/resubscribed");
		if (device.GetType().Equals(typeof(HeartRate))){
			m_heartrate = (HeartRate)device;
			m_heartrate.Sync(b_Loggin);
		}
		m_status  = "syncing...";
		if (b_Loggin)
			Debug.Log("HeartRateScene: Device successfully connected, syncing");
	}
	
	private void OnDeviceDisconnected(int reason, BleDevice d) {
		BluetoothLEPlugin.BleReadyForSyncEvent -= OnReadyForSync;
		//s_LabelText = "";
		m_shouldtick = false;
		m_currentDevice = null;
		if (b_Loggin)
			Debug.Log("HeartRateScene: got OnDeviceDisconnected with reason: " +(disconnectReason)reason +"("+reason+")\nBleReadyForSyncEvent unsubscribed");
		if (reason == 8){//manual Disconnect
			m_status = "Disconnected\nFor new Measurement\npress Update";
			BluetoothLEPlugin.BleDeviceFoundEvent -= OnDeviceFound;
			BluetoothLEPlugin.BleDeviceFoundEvent += OnDeviceFound;
			if (b_Loggin)
				Debug.Log("HeartRateScene: BleDeviceFoundEvent un-/resubscribed");
			m_state = MenuState.START;
			//m_state = MenuState.SCANNING;
			//BluetoothLEPlugin.Scan(90);
		} else {
			m_status = "Got Disconnected-Event for:\n" + d.Adress + "\nreason: " +(disconnectReason)reason + "\nplease check and hit update";
			clearMAC();
			m_state = MenuState.START;
		}
	}
	
	private void ControlPointFeatured(){
		if (b_Loggin)
			Debug.Log("HeartRateScene: got ControlPointFeatured");
//		b_ControlPointFeatured = true;
		//m_heartrate.writeControlPoint();
	}
	
	private void On_DataReceived() {
		m_status = "SampleScene: Data handled, displaying data";
		displayData();
	}
	
	private void displayData(){
		m_status = "Connected and syncing\nTo stop press Disconnect";
		HeartRate_Measurement Measurement = m_heartrate.GetHeartRateMeasurement();
		m_shouldtick = true;
		m_currentrate = Measurement.pulsrate;
		s_LabelText =
			"Pulsrate       : " + Measurement.pulsrate + " bpm" + "\n" +
				"Sensor Location: " + (HRM_BodySensorLocation)Measurement.SensorLocation + "\n" +
				"Sensor Contact : " + (HRM_SensorContactStatus)Measurement.SCStatus;
		if (Measurement.energyExpended == 0){
			s_LabelText = s_LabelText + "\n" +
				"Energy Expended: " + (HRM_EnergyExpended)Measurement.energyExpended;
		} else {
			s_LabelText = s_LabelText + "\n" +
				"Energy Expended: " + (HRM_EnergyExpended)Measurement.energyExpended + " kJ";
		}
		if( Measurement.rrInterval[0] == (HRM_EnergyExpended)0){
			s_LabelText = s_LabelText + "\n" +
				"RR-Interval    : " + (HRM_EnergyExpended)Measurement.rrInterval[0];
		} else {
			for(int i=0;i<(int)Measurement.rrInterval[0];i++){
				s_LabelText = s_LabelText + "\n" +
					"RR-Interval["+(i+1)+"] : " + (HRM_EnergyExpended)Measurement.rrInterval[i+1] + " ms";
			}
		}
	}
	
	public void ConnectDevice(BleDevice device) {
		if (b_Loggin)
			Debug.Log("HeartRateScene: Connect-Button hit");
		BluetoothLEPlugin.BleDeviceFoundEvent -= OnDeviceFound;
		if (b_Loggin)
			Debug.Log("HeartRateScene: BleDeviceFoundEvent unsubscribed");
		m_status  = "connecting...";
		BluetoothLEPlugin.BleDeviceConnectEvent -= OnDeviceConnected;
		BluetoothLEPlugin.BleDeviceConnectEvent += OnDeviceConnected;
		if (b_Loggin)
			Debug.Log("HeartRateScene: BleDeviceConnectEvent un-/resubscribed");
		device.Connect();
		if (m_adresses != null)
			m_adresses.Clear();
	}

	void Update() {
		if (m_state == MenuState.CONNECTED) {
			if (!m_shouldtick)
				return;
			if (m_tickrate<0){
				m_tickrate = 359;
			}
			List<Vector3> temp = new List<Vector3>();
			if (m_tickrate < 180){
				temp.Add(new Vector3(1*.1f, m_tickrate*.022f,1));
			}
			else {
				temp.Add(new Vector3(1*.1f, (359-m_tickrate)*.022f,1));
			}
			for (int i = 0; i < 499; i++) {
				temp.Add (new Vector3(i_DotArray[i].x+.1f,i_DotArray[i].y,0));
			}
			i_DotArray = temp;
			for (int j = 0; j < i_DotArray.Count; j++) {
				lr.SetPosition(j,i_DotArray[j]);	
			}
			m_tickrate -= 1000*Time.deltaTime*m_currentrate/120;
		}
	}
}