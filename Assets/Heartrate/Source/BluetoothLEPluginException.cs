using UnityEngine;
using System;
using System.Collections;

public class BluetoothLEPluginException : Exception {
	public BluetoothLEPluginException ()
	{
		
	}
	
	public BluetoothLEPluginException (string message) : base(message)
	{
		
	}
	
	public BluetoothLEPluginException (string message,Exception inner) : base(message, inner) {
		
	}
}