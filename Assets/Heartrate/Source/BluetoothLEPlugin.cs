using UnityEngine;
using System;
using System.Collections;
using System.Text;
using System.Runtime.InteropServices;

public enum scanReason{
	SCAN_READY						= 0,
	NOT_SUPPORTED					= 1,
	NOT_AVAILABLE					= 2,
	POWERED_OFF						= 3,
	TIMEOUT							= 4
}

public enum disconnectReason{
	DESCRIPTOR						= 5,
	DEVICE_NOT_AVAILABLE			= 6,
	DEVICE_DID_DISCONNECT			= 7,
	MANUAL_DISCONNECT				= 8,
	DISCOCERSERVICE					= 9,
	CBErrorUnknown					= 10,
	CBErrorInvalidParameters		= 11,
	CBErrorInvalidHandle			= 12,
	CBErrorNotConnected				= 13,
	CBErrorOutOfSpace				= 14,
	CBErrorOperationCancelled		= 15,
	CBErrorConnectionTimeout		= 16,//after Transmission at OxyMeter
	CBErrorPeripheralDisconnected	= 17,
	CBErrorUUIDNotAllowed			= 18,
	CBErrorAlreadyAdvertising		= 19,
	DEVICE_DID_NOT_RESPOND			= 20,
	DEVICE_BATTERY_LOW				= 21,
	SUCCESSFUL_MEASUREMENT			= 22
}

public enum DeviceType {
	Unknown,
	HeartRateSensor,
}

public class BluetoothLEPlugin : MonoBehaviour {
#region Events
	public delegate void	BleValueChange(string receivedData);
	public static event		BleValueChange			BleValueChangeEvent;
	public delegate void	BleDeviceFound(BleDevice device);
	public static event		BleDeviceFound			BleDeviceFoundEvent;
	public delegate void	BleDeviceConnect(BleDevice device);
	public static event		BleDeviceConnect		BleDeviceConnectEvent;
	public delegate void	BleDeviceDisconnect(int reason, BleDevice device);
	public static event		BleDeviceDisconnect		BleDeviceDisconnectEvent;
	public delegate void	BleDeviceScan(int reason,bool StartScanAgain);
	public static event		BleDeviceScan			BleDeviceScanEvent;
	public delegate void	BleReadyForSync(BleDevice device);
	public static event		BleReadyForSync			BleReadyForSyncEvent;
#endregion

#region Plugin import
	#if UNITY_ANDROID
		private static AndroidJavaObject _plugin;
	#elif UNITY_IPHONE && !UNITY_EDITOR	
		[DllImport ("__Internal")]
		private static extern void InitpluginiOS(bool shouldLog);
		
		[DllImport ("__Internal")]
		private static extern void StopDeviceiOS();
		
		[DllImport ("__Internal")]
		private static extern int GetBLEStatusiOS();
		
		[DllImport ("__Internal")]
		private static extern void ScaniOS(string device,int secondstoscan);
		
		[DllImport ("__Internal")]
		private static extern void StopScaniOS();
		
		[DllImport ("__Internal")]
		private static extern void ConnectiOS(string uuid, string devicetype);
		
		[DllImport ("__Internal")]
		private static extern void SendBytesToDeviceiOS(byte[] bytes, int length, string prefix, bool includedatacounts, string device);
		
		[DllImport ("__Internal")]
		private static extern void AskForFeaturesiOS(string device);
		
		[DllImport ("__Internal")]
		private static extern void DisconnectiOS();

		[DllImport ("__Internal")]
		private static extern void DisconnectWithReasoniOS(string reason);
		
		[DllImport ("__Internal")]
		private static extern void EnableBluetoothiOS();
	#elif UNITY_STANDALONE_OSX || UNITY_EDITOR
		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public delegate void AsyncCallback(string callbackString);

		[DllImport ("Ble_Device_MAC")]
		private static extern void InitpluginMacOS(bool Log,
		AsyncCallback ReportDisconnect,
		AsyncCallback ReportActualBleState,
		AsyncCallback ReportMacAdressFound,
		AsyncCallback ReportConnect,
		AsyncCallback ReportHRControlPointFeatured,
		AsyncCallback ReadyForSync,
		AsyncCallback LogBleDevice);
			
		[DllImport ("Ble_Device_MAC")]
		private static extern int GetBLEStatusMacOS();
			
		[DllImport ("Ble_Device_MAC")]
		private static extern void ScanMacOS(string device, int secondstoscan);
			
		[DllImport ("Ble_Device_MAC")]
		private static extern void StopScanMacOS();

		// onSceneDisabled
		[DllImport ("Ble_Device_MAC")]
		private static extern void StopDeviceMacOS();
			
		[DllImport ("Ble_Device_MAC")]
		private static extern void ConnectMacOS(string uuid, string devicetype);
			
		[DllImport ("Ble_Device_MAC")]
		private static extern void SendBytesToDeviceMacOS(byte[] bytes, int length, string prefix, bool includedatacounts, string device);
			
		[DllImport ("Ble_Device_MAC")]
		private static extern void AskForFeaturesMacOS(string device);
			
		[DllImport ("Ble_Device_MAC")]
		private static extern void DisconnectMacOS();

		[DllImport ("Ble_Device_MAC")]
		private static extern void DisconnectWithReasonMacOS(string reason);
	#endif
#endregion

#region Variables
	private static BleDevice					m_currentdevice;
	private static bool							b_Loggin;
	private static BluetoothLEPlugin			m_instance;
	private static bool							m_initialized;
	private static bool							m_isInitializedForScan;
#endregion

	void Awake() {
		m_instance = this;
		Debug.Log("BluetoothLEPlugin.cs; Awake: m_instance: " + m_instance);
	}

	public static void Initialize(bool shouldLog) {
		if (!m_initialized) {
			b_Loggin = shouldLog;
			if (b_Loggin)
				Debug.Log ("BluetoothLEPlugin.cs; Initialize");
			#if UNITY_ANDROID
				AndroidJavaClass jc = new AndroidJavaClass("com.kaasa.blepluginandroid.Ble_Device_Android");
				AndroidJavaObject temp = jc.CallStatic<AndroidJavaObject>("instance");
				_plugin = temp;
				_plugin.Call("InitPluginAndroid",shouldLog);	
			#elif UNITY_IPHONE && !UNITY_EDITOR
				InitpluginiOS(shouldLog);	
			#elif UNITY_STANDALONE_OSX || UNITY_EDITOR
				if (b_Loggin)
					Debug.Log ("BluetoothLEPlugin.cs; shouldLog: " + shouldLog);
			InitpluginMacOS(shouldLog, ReportDisconnect, ReportActualBleState, ReportMacAdressFound, ReportConnect, ReportHRControlPointFeatured, ReadyForSync, LogBleDevice);
			#endif
			m_initialized = true;
		}
	}

	public static void Scan(int SecondsToScan) {
		m_isInitializedForScan = true;
		string sb = "180d,";
		if (b_Loggin)
			Debug.Log ("BluetoothLEPlugin.cs; got Scan-command, checking BLE-status");
		#if UNITY_ANDROID
			if (!_plugin.Call<bool>("IsBleFeatured") || !_plugin.Call<bool>("IsBluetoothAvailable") || !_plugin.Call<bool>("IsBluetoothTurnedOn")) {
				throw new BluetoothLEPluginException("Scan not possible");
			} else {
				if(BleDeviceScanEvent != null)
					BleDeviceScanEvent(0,false);
				_plugin.Call("Scan",sb,SecondsToScan);
			}
		#elif UNITY_IPHONE && !UNITY_EDITOR
			if (GetBLEStatusiOS() != 5) {
				throw new BluetoothLEPluginException("Scan not possible");
			} else {
				if (BleDeviceScanEvent != null) {
					BleDeviceScanEvent(0,false);	
				}
				ScaniOS(sb.ToString(),SecondsToScan);
			}
		#elif UNITY_STANDALONE_OSX || UNITY_EDITOR
			if (GetBLEStatusMacOS() != 5) {
				throw new BluetoothLEPluginException("Scan not possible");
			} else {
				if (BleDeviceScanEvent != null) {
					BleDeviceScanEvent(0,false);	
				}
				ScanMacOS(sb.ToString(),SecondsToScan);
			}
		#endif
	}

	#if UNITY_STANDALONE_OSX || UNITY_EDITOR
	public static void ReportScan(string s_reason) {
	#else
	public void ReportScan(string s_reason) {
	#endif
		int reason = int.Parse(s_reason);
		if (b_Loggin)
			Debug.Log("BluetoothLEPlugin.cs; Scan stopped due to "+(scanReason)reason);
		if (BleDeviceScanEvent != null)
			BleDeviceScanEvent(reason, false);
	}

	public static void StopScan() {
		#if UNITY_ANDROID
			_plugin.Call("StopScan");
		#elif UNITY_IPHONE && !UNITY_EDITOR
			StopScaniOS();
		#elif UNITY_STANDALONE_OSX || UNITY_EDITOR
			StopScanMacOS();
		#endif
	}

	public static void CheckBLEStatus() {
		if (b_Loggin)
			Debug.Log ("BluetoothLEPlugin.cs; checking Ble status");
		#if UNITY_ANDROID
			if (!_plugin.Call<bool>("IsBleFeatured")) {
				if (BleDeviceScanEvent != null)
					BleDeviceScanEvent(1,false);
			} else {
				if (!_plugin.Call<bool>("IsBluetoothAvailable")) {
					if (BleDeviceScanEvent != null)
						BleDeviceScanEvent(2,false);
				} else {
					if (!_plugin.Call<bool>("IsBluetoothTurnedOn")) {
						if (BleDeviceScanEvent != null)
							BleDeviceScanEvent(3,false);
				} else {//never called
						if (BleDeviceScanEvent != null)
							BleDeviceScanEvent(0,false);
					}
				}
			}
		#elif UNITY_IPHONE && !UNITY_EDITOR
			switch (GetBLEStatusiOS()) {
			case 0:
			case 1:
				break;
			case 2://not supported
				if (BleDeviceScanEvent != null)
					BleDeviceScanEvent(1,false);
				break;
			case 3://not available
				if (BleDeviceScanEvent != null)
					BleDeviceScanEvent(2,false);
				break;
			case 4://powered off
				if (BleDeviceScanEvent != null)
					BleDeviceScanEvent(3,false);
				break;
			case 5://never called
				if (BleDeviceScanEvent != null)
					BleDeviceScanEvent(0,false);
				break;
			}
		#elif UNITY_STANDALONE_OSX || UNITY_EDITOR
			switch (GetBLEStatusMacOS()) {
			case 0:
			case 1:
				break;
			case 2://not supported
				if (BleDeviceScanEvent != null)
					BleDeviceScanEvent(1,false);
				break;
			case 3://not available
				if (BleDeviceScanEvent != null)
					BleDeviceScanEvent(2,false);
				break;
			case 4://powered off
				if (BleDeviceScanEvent != null)
					BleDeviceScanEvent(3,false);
				break;
			case 5://powered on, never called
				if (BleDeviceScanEvent != null)
					BleDeviceScanEvent(0,false);
				break;
			}
		#endif
	}

	public static void EnableBluetooth() {
		if (b_Loggin)
			Debug.Log ("BluetoothLEPlugin.cs; request for enabling Bluetooth");
		#if UNITY_ANDROID
			_plugin.Call("EnableBluetooth");
		#elif UNITY_IPHONE && !UNITY_EDITOR
			EnableBluetoothiOS();
		#endif
	}

	public static void RestartBluetooth(){
		#if UNITY_ANDROID
			_plugin.Call("RestartBluetooth");
		#endif
	}

	//Event, if Ble-status did change
	#if UNITY_STANDALONE_OSX || UNITY_EDITOR
	public static void ReportActualBleState(string s_actualState) {
	#else
	public void ReportActualBleState(string s_actualState) {
	#endif
		Debug.Log("BluetoothLEPlugin.cs; ReportActualBleState: state: " + s_actualState);
		int i_actualState = int.Parse(s_actualState);
		if (m_isInitializedForScan) {
			if (i_actualState != 5) {
				//Choice to keep Ble powered off
				if (b_Loggin)
					Debug.Log("BluetoothLEPlugin.cs; user decided not to turn on Bluetooth. Firing Event: BleDeviceScanEvent(" + (scanReason)3 + ", false)");
				if (BleDeviceScanEvent != null)
					BleDeviceScanEvent(3,false);
			} else {
				//Choice to turn on Bluetooth
				if (b_Loggin)
					Debug.Log("BluetoothLEPlugin.cs; User decided to turn on Bluetooth. Firing: BleDeviceScanEvent(0,true");
				if (BleDeviceScanEvent != null)
					BleDeviceScanEvent(0,true);
			}
			//actualBleState = i_actualState;
		}
	}

	#if UNITY_STANDALONE_OSX || UNITY_EDITOR
	public static void ReportMacAdressFound(string s_DeviceMACAdress) {
	#else
	public void ReportMacAdressFound(string s_DeviceMACAdress) {
	#endif
		if (b_Loggin)
			Debug.Log("BluetoothLEPlugin.cs; ReportMacadressFound called with s_DeviceMACAdress: " + s_DeviceMACAdress);
		string[] splitString = s_DeviceMACAdress.Split(',');
		string deviceservuuid = splitString[3];
		string name = splitString[2];
		string adress = splitString[0];
		int devicetype = Int32.Parse(splitString[1]);
		if (b_Loggin)
			Debug.Log("BluetoothLEPlugin.cs; deviceservuuid: "+deviceservuuid+", name: "+name+", adress: "+adress+", devicetype: "+devicetype);
		BleDevice device;
		switch((DeviceType)devicetype) {
		case DeviceType.HeartRateSensor://1
			device = new HeartRate(name,adress,deviceservuuid,b_Loggin);
			break;
		default:
			device = new BleDevice(name,adress,deviceservuuid,b_Loggin);
			break;
		}
		if (b_Loggin)
			Debug.Log("BluetoothLEPlugin.cs; got MAC-Adress, sending BleDeviceFoundEvent("+s_DeviceMACAdress+")");
		if (BleDeviceFoundEvent != null) {
			BleDeviceFoundEvent(device);
		}
	}

	public static void Connect(BleDevice d) {
		m_instance.StartCoroutine("TimeoutDisconnect");
		m_currentdevice = d;
		if (b_Loggin)
			Debug.Log ("BluetoothLEPlugin.cs; got Connect-Command, calling Connect-Method with adress: "+d.Adress+" and devicetype: "+d.GetType());
		#if UNITY_ANDROID
			_plugin.Call("Connect",d.Adress,d.GetType().ToString());
		#elif UNITY_IPHONE && !UNITY_EDITOR
			ConnectiOS(d.Adress,d.GetType().ToString());
		#elif UNITY_STANDALONE_OSX || UNITY_EDITOR
			ConnectMacOS(d.Adress,d.GetType().ToString());
		#endif
	}

	#if UNITY_STANDALONE_OSX || UNITY_EDITOR
	public static void ReportConnect(string macadress) {
	#else
	public void ReportConnect(string macadress) {
	#endif
		if (b_Loggin)
			Debug.Log("BluetoothLEPlugin.cs; stopping connect-timer\ngot MAC-Adress with "+m_currentdevice.Adress+" via ReportConnect,\nfiring BleDeviceConnectEvent(" + macadress + ")");
		m_instance.StopCoroutine("TimeoutDisconnect");
		if (BleDeviceConnectEvent != null)
			BleDeviceConnectEvent(m_currentdevice);
	}

	private IEnumerator TimeoutDisconnect() {
		if (b_Loggin)
			Debug.Log ("BluetoothLEPlugin.cs; starting connect-timer");
		yield return new WaitForSeconds(7.0f);
		if (b_Loggin)
			Debug.Log("BluetoothLEPlugin.cs; Timeout on connect-timer");
		DisconnectWithReason("20");
	}

	public static void Disconnect() {
		if (b_Loggin)
			Debug.Log ("BluetoothLEPlugin.cs; got Disconnect-command, calling Disconnect-Method");
		#if UNITY_ANDROID
			_plugin.Call("Disconnect");
		#elif UNITY_IPHONE && !UNITY_EDITOR
			DisconnectiOS();
		#elif UNITY_STANDALONE_OSX || UNITY_EDITOR
			DisconnectMacOS();
		#endif
	}

	public static void DisconnectWithReason(string reason) {
		if (b_Loggin)
			Debug.Log ("BluetoothLEPlugin.cs; got Disconnect on Timeout, calling Disconnect-Method");
		#if UNITY_ANDROID
			_plugin.Call("Disconnect",reason);
		#elif UNITY_IPHONE && !UNITY_EDITOR
			DisconnectWithReasoniOS(reason);
		#elif UNITY_STANDALONE_OSX || UNITY_EDITOR
			DisconnectWithReasonMacOS(reason);
		#endif
	}

	#if UNITY_STANDALONE_OSX || UNITY_EDITOR
	public static void ReportDisconnect(string s_reason) {
	#else
	public void ReportDisconnect(string s_reason) {
	#endif
		int reason = int.Parse(s_reason);
		if (b_Loggin)
			Debug.Log("BluetoothLEPlugin.cs; got disconnect-message via ReportDisconnect, firing BleDeviceDisconnectEvent("+(disconnectReason)reason+")\nstopping connect timer");
		m_instance.StopCoroutine("TimeoutDisconnect");
		m_instance.StartCoroutine(m_instance.SendDelayedReportDisconnect(reason,m_currentdevice));
	}
	
	private IEnumerator SendDelayedReportDisconnect(int reason, BleDevice device) {//needed for Bloodpressure and WeightScale
		if (b_Loggin)
			Debug.Log("BluetoothLEPlugin.cs; delayed disconnect");
		yield return new WaitForSeconds(1.5f);
		if (BleDeviceDisconnectEvent != null)
			BleDeviceDisconnectEvent(reason,device);
	}
		
	#if UNITY_STANDALONE_OSX || UNITY_EDITOR
	public static void ReadyForSync(string macadress) {
	#else
	public void ReadyForSync(string macadress) {
	#endif
		if (b_Loggin)
			Debug.Log("BluetoothLEPlugin.cs; got ReadyForSync with "+m_currentdevice.Adress+" via Nativebridge,\nfiring BleReadyForSyncEvent(" + macadress + ")");
		if (BleReadyForSyncEvent != null)
			BleReadyForSyncEvent(m_currentdevice);
	}

	public static void gotFeatures(string Device) {
		if (b_Loggin)
			Debug.Log ("BluetoothLEPlugin.cs; got gotFeatures-command with " + Device);
		#if UNITY_ANDROID
			_plugin.Call("gotFeatures",Device);
		#elif UNITY_IPHONE && !UNITY_EDITOR
			AskForFeaturesiOS(Device);
		#elif UNITY_STANDALONE_OSX || UNITY_EDITOR
			AskForFeaturesMacOS(Device);
		#endif
	}
		
	#if UNITY_STANDALONE_OSX || UNITY_EDITOR
	public static void LogBleDevice(string receivedValue) {
	#else
	public void LogBleDevice(string receivedValue) {
	#endif
		if (b_Loggin)
			Debug.Log("BluetoothLEPlugin.cs; Got Value: " + receivedValue);
		m_instance.StartCoroutine(m_instance.SendDelayedLogBleDevice(receivedValue));
	}
	
	private IEnumerator SendDelayedLogBleDevice(string receivedValue) {
		yield return new WaitForSeconds(0.3f);		
		if (BleValueChangeEvent != null){
			Debug.Log("BluetoothLEPlugin.cs; sending delayed value to class");
			BleValueChangeEvent(receivedValue.ToUpper());
		}
		yield return 0;
	}
	
	public static void Stop(){
		#if UNITY_ANDROID
		if (Application.platform == RuntimePlatform.Android) {
			_plugin.Call("scanLeDevice",false);
			_plugin.Call("Disconnect");
		}
		#elif UNITY_IPHONE && !UNITY_EDITOR
			StopDeviceiOS();
		#elif UNITY_STANDALONE_OSX || UNITY_EDITOR
			StopDeviceMacOS();
		#endif
	}
	
	void OnApplicationQuit(){
		#if UNITY_ANDROID
			if (Application.platform == RuntimePlatform.Android)
				_plugin.Call("scanLeDevice",false);
		#elif UNITY_IPHONE && !UNITY_EDITOR
			StopDeviceiOS();
		#elif UNITY_STANDALONE_OSX || UNITY_EDITOR
			StopDeviceMacOS();
		#endif
	}

#region HeartRate specific
	public delegate void	HRControlPointFeature();
	public static event		HRControlPointFeature HRControlPointFeatureEvent;

	#if UNITY_STANDALONE_OSX || UNITY_EDITOR
	public static void ReportHRControlPointFeatured(string featured) {
	#else
	public void ReportHRControlPointFeatured(string featured) {
	#endif
		if (b_Loggin)
			Debug.Log("BluetoothLEPlugin.cs; Got HR_ControlPoint-feature");
		if (HRControlPointFeatureEvent != null)
			HRControlPointFeatureEvent();
	}

	public static void sendHR_ControlPoint(byte[] ControlPoint_Cmd){
		if (b_Loggin)
			Debug.Log ("BluetoothLEPlugin.cs; sending ControlPoint_Cmd to HeartRate");
		#if UNITY_ANDROID
			_plugin.Call("HeartRateCommand", ControlPoint_Cmd);	
		#elif UNITY_IPHONE && !UNITY_EDITOR
			SendBytesToDeviceiOS(ControlPoint_Cmd,1,"2a39 ",false,"heartrate");//TODO: remove argument 2,3 and 4 ?
		#elif UNITY_STANDALONE_OSX || UNITY_EDITOR
			SendBytesToDeviceMacOS(ControlPoint_Cmd,1,"2a39 ",false,"heartrate");//TODO: remove argument 2,3 and 4 ?
		#endif
	}
#endregion
}