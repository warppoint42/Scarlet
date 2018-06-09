using UnityEngine;
using System.Collections;

public class BleDevice {
	public string	Name {get;protected set;}
	public string	Adress {get;protected set;}
	public string	DeviceServUUID {get;protected set;}
	public bool		Connected {get;protected set;}
	protected bool	IsDebug = false;

	public BleDevice (string _name, string _adress, string _deviceServUUID, bool _isDebug){
		if (IsDebug)
			Debug.Log("BleDevice: Created "+GetType().ToString()+" "+Name+"with Adress "+Adress+" and UUID "+DeviceServUUID);
		Name			= _name;
		Adress			= _adress;
		IsDebug			= _isDebug;
		DeviceServUUID	= _deviceServUUID ;
	}

	public virtual void Connect() {
		if (IsDebug)
			Debug.Log("BleDevice: Connecting "+GetType().ToString()+" "+Name+", Adress: "+Adress+", UUID: "+DeviceServUUID);
		BluetoothLEPlugin.BleDeviceDisconnectEvent -= OnDeviceDisconnected;//keep here!!!
		BluetoothLEPlugin.BleDeviceDisconnectEvent += OnDeviceDisconnected;
		BluetoothLEPlugin.BleDeviceConnectEvent -= OnDeviceConnected;
		BluetoothLEPlugin.BleDeviceConnectEvent += OnDeviceConnected;
		if (IsDebug)
			Debug.Log("BleDevice: BleDeviceDisconnectEvent and BleDeviceConnectEvent un-/resubscribed");
		BluetoothLEPlugin.BleValueChangeEvent -= OnDataReceived;
		BluetoothLEPlugin.BleValueChangeEvent += OnDataReceived;
		if (IsDebug)
			Debug.Log("BleDevice: BleValueChangeEvent un-/resubscribed");
		BluetoothLEPlugin.Connect(this);
	}
	
	public virtual void Reset() {
		if (IsDebug)
			Debug.Log("BleDevice: Resetting "+GetType().ToString()+" "+Name+", Adress: "+Adress);
		BluetoothLEPlugin.BleDeviceConnectEvent -= OnDeviceConnected;
		BluetoothLEPlugin.BleDeviceDisconnectEvent -= OnDeviceDisconnected;
		BluetoothLEPlugin.BleValueChangeEvent -= OnDataReceived;
		if (IsDebug)
			Debug.Log("BleDevice: BleDeviceConnectEvent, BleDeviceDisconnectEvent and BleValueChangeEvent unsubscribed");
	}
	
	public virtual void Disconnect() {
		if (IsDebug)
			Debug.Log("BleDevice: Disconnecting "+GetType().ToString()+" "+Name+", Adress: "+Adress);
		BluetoothLEPlugin.Disconnect();
		BluetoothLEPlugin.BleDeviceConnectEvent -= OnDeviceConnected;
		BluetoothLEPlugin.BleValueChangeEvent -= OnDataReceived;
		if (IsDebug)
			Debug.Log("BleDevice: BleDeviceConnectEvent and BleValueChangeEvent unsubscribed");
	}

	protected virtual void OnDeviceConnected(BleDevice device) {
		Connected = true;
		if (IsDebug)
			Debug.Log("BleDevice: Connected "+GetType().ToString()+" "+Name+", Adress: "+Adress);
		/*BluetoothLEPlugin.BleDeviceDisconnectEvent -= OnDeviceDisconnected;
		BluetoothLEPlugin.BleDeviceDisconnectEvent += OnDeviceDisconnected;
		if (IsDebug)
			Debug.Log("BleDevice: BleDeviceDisconnectEvent un-/resubscribed");*/
	}

	protected virtual void OnDeviceDisconnected(int reason, BleDevice device) {
		Connected = false;
		if (IsDebug)
			Debug.Log("BleDevice: Disconnected "+GetType().ToString()+" "+Name+", Adress: "+Adress+", Reason: "+((disconnectReason)reason).ToString());
		BluetoothLEPlugin.BleDeviceDisconnectEvent -= OnDeviceDisconnected;
		BluetoothLEPlugin.BleValueChangeEvent -= OnDataReceived;
		if (IsDebug)
			Debug.Log("BleDevice: BleDeviceDisconnectEvent and BleValueChangeEvent unsubscribed");
	}
	
	protected virtual void OnDataReceived(string receivedData) {
		if (IsDebug)
			Debug.Log("BleDevice: Received Data: "+receivedData);
	}
}