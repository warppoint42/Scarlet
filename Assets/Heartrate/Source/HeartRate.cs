#define HEARTRATE_DEBUG
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;//for List
//using System.Text;

public enum HRM_SensorContactStatus {
	NOT_SUPPORTED_0	= 0,
	NOT_SUPPORTED_1	= 1,
	NO_CONTACT		= 2,
	CONTACT			= 3
}

public enum HRM_BodySensorLocation {
	OTHER			= 0,
	CHEST			= 1,
	WRIST			= 2,
	FINGER			= 3,
	HAND			= 4,
	EARLOBE			= 5,
	FOOT			= 6
}

public enum HRM_EnergyExpended : uint {
	NOT_PRESENT		= 0
}

public struct HeartRate_Measurement {//"0x2A37"
	public uint						pulsrate;
	public HRM_EnergyExpended		energyExpended;
	public List<HRM_EnergyExpended> rrInterval;
	public HRM_SensorContactStatus	SCStatus;
	public HRM_BodySensorLocation	SensorLocation;
	public bool						initialized;
	public HeartRate_Measurement (uint _pulsrate, HRM_EnergyExpended _energyExpended, List<HRM_EnergyExpended> _rrInterval, HRM_SensorContactStatus _SCStatus, HRM_BodySensorLocation _SensorLocation)
	{
		initialized		= true;
		pulsrate		= _pulsrate;
		energyExpended	= _energyExpended;
		rrInterval		= _rrInterval;
		SCStatus		= _SCStatus;
		SensorLocation	= _SensorLocation;
	}
}

public class HeartRate : BleDevice {

#region delegates and events
	public delegate void HRMDataReceived();
	public static event HRMDataReceived OnHRMDataReceived;
#endregion
#region variables
	private bool					b_Loggin;
	private HeartRate_Measurement	HRM;
#endregion
	
	public HeartRate (string _name, string _adress,string _deviceServUUID, bool _isdebug) : base(_name,_adress,_deviceServUUID,_isdebug)
	{}
	
	public override void Disconnect() {
		if (b_Loggin)
			Debug.Log("HeartRate.cs: disconnecting base");
		base.Disconnect();
	}

	public override void Reset() {
		base.Reset();
		if (IsDebug)
			Debug.Log("Pedometer.cs: reset done");
	}

	public void Sync(bool Log) {
		b_Loggin = Log;
		if (b_Loggin)
			Debug.Log("HeartRate.cs: got Sync-Command from Scene");
		BluetoothLEPlugin.gotFeatures("heartrate");
	}

#region Response
	protected override void OnDataReceived(string receivedData) {
		base.OnDataReceived(receivedData);
		string[] splittedReceivedData = receivedData.ToUpper().Split(new Char [] {' '});
		if (b_Loggin)
			Debug.Log("HeartRate.cs: gotData-method called. Handling case: " + splittedReceivedData[0]);
		//HRM = new HeartRate_Measurement();
		switch(splittedReceivedData[0]) {
		case "2A38"://Body Sensor Location
			if (b_Loggin)
				Debug.Log ("HeartRate.cs: got Body Sensor Location");
			HRM.SensorLocation = (HRM_BodySensorLocation)Convert.ToDecimal(splittedReceivedData[1]);//UInt32
			if (b_Loggin)
				Debug.Log ("HeartRate.cs: Body Sensor Location: " + (HRM_BodySensorLocation)HRM.SensorLocation);
			break;
		case "2A37"://HeartRate Measurement
			if (b_Loggin)
				Debug.Log ("HeartRate.cs: got HRM-measurement-Data, starting handling");
			int RR_Interval_Values						= splittedReceivedData.Length-1;//-1 because Bridge added an space at the end of the string
			int Offset									= 2;
			string binaryVal							= Convert.ToString(Convert.ToUInt32(splittedReceivedData[1], 16),2);
			binaryVal									= binaryVal.PadLeft(8,'0');
			char[] arr									= binaryVal.ToCharArray();
			Array.Reverse(arr);
			binaryVal = new string(arr);
			if (binaryVal[0] == '0') {	//Pulsrate in UINT8
				if (b_Loggin)
					Debug.Log ("HeartRate.cs: Pulsrateformat is: UINT8");
				HRM.pulsrate							= Convert.ToUInt32(splittedReceivedData[Offset],16);
				Offset += 1;
			} else {					//Pulsrate in UINT16
				if (b_Loggin)
					Debug.Log ("HeartRate.cs: Pulsrateformat is: UINT16");
				HRM.pulsrate							= Convert.ToUInt32(splittedReceivedData[Offset],16) + 256*Convert.ToUInt32(splittedReceivedData[Offset+1],16);
				Offset += 2;
			}
			Debug.Log ("HeartRate.cs: Pulsrate: " + HRM.pulsrate + " bpm");
			//Sensor Conact
			HRM.SCStatus								= (HRM_SensorContactStatus)(2*Convert.ToDecimal(binaryVal[1].ToString()) + Convert.ToDecimal(binaryVal[2].ToString()));
			if (b_Loggin)
				Debug.Log ("HeartRate.cs: Sensor Contact: " + (HRM_SensorContactStatus)HRM.SCStatus);
			//Energy Expended
			if (binaryVal[3] == '1') {
				HRM.energyExpended						= (HRM_EnergyExpended)(Convert.ToUInt32(splittedReceivedData[Offset],16) + 256*Convert.ToUInt32(splittedReceivedData[Offset+1],16));
				Offset += 2;
			} else {
				HRM.energyExpended						= (HRM_EnergyExpended)0;
			}
			if (b_Loggin)
				Debug.Log ("HeartRate.cs: Energy Expended: " + (HRM_EnergyExpended)HRM.energyExpended);
			//RR-Interval
			HRM.rrInterval = new List<HRM_EnergyExpended>();
			if (binaryVal[4] == '1') {
				RR_Interval_Values = (RR_Interval_Values-Offset)/2;
				if (b_Loggin)
					Debug.Log ("HeartRate.cs: RR_Interval_Value_Count: " + RR_Interval_Values.ToString());
				HRM.rrInterval.Add((HRM_EnergyExpended) RR_Interval_Values);
				for (int i=0;i<RR_Interval_Values;i++){
					HRM.rrInterval.Add((HRM_EnergyExpended)(1000*(Convert.ToUInt32(splittedReceivedData[Offset+(i*2)],16) + 256*Convert.ToUInt32(splittedReceivedData[(Offset+1)+(i*2)],16))/1024));
					if(b_Loggin)
						Debug.Log ("HeartRate.cs: RR_Interval["+(i+1)+"]: " + HRM.rrInterval[i+1]);
				}
			} else {
				HRM.rrInterval.Add((HRM_EnergyExpended)0);
				if (b_Loggin)
					Debug.Log ("HeartRate.cs: RR-Interval values are " + (HRM_EnergyExpended)HRM.rrInterval[0]);
			}
			/***** End of Datahandling *****/
			if (b_Loggin)
				Debug.Log ("HeartRate.cs: data handling done, sending event");
			if (OnHRMDataReceived != null)
				OnHRMDataReceived();
			break;
		default:
			throw new BluetoothLEPluginException("No matching data type received");
		}
	}
#endregion

#region write
	public void writeControlPoint() {
		if (b_Loggin)
			Debug.Log("HeartRate.cs: got ControlPoint-Command from Scene, sending to Device");
		byte[] ControlPoint_Cmd = {(byte)0x01};
		BluetoothLEPlugin.sendHR_ControlPoint(ControlPoint_Cmd);
	}
#endregion

#region getData
	public HeartRate_Measurement GetHeartRateMeasurement() {
		return HRM;	
	}
#endregion
}

