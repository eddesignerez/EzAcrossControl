package com.example.ezacrosscontrol.control

import android.accessibilityservice.AccessibilityService
import android.content.Intent
import android.view.accessibility.AccessibilityEvent
import android.util.Log

class EZAcrossAccessibilityService : AccessibilityService() {
    
    override fun onServiceConnected() {
        super.onServiceConnected()
        Log.i("EZAcrossAccessibility", "Service connected")
        AndroidControlManager.onAccessibilityServiceConnected(this)
    }

    override fun onAccessibilityEvent(event: AccessibilityEvent?) {
        // We don't need to process UI events for now
    }

    override fun onInterrupt() {
        Log.w("EZAcrossAccessibility", "Service interrupted")
        AndroidControlManager.onAccessibilityServiceDisconnected()
    }

    override fun onUnbind(intent: Intent?): Boolean {
        Log.i("EZAcrossAccessibility", "Service unbound")
        AndroidControlManager.onAccessibilityServiceDisconnected()
        return super.onUnbind(intent)
    }
}
