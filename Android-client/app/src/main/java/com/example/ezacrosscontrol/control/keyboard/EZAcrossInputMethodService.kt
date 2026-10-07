package com.example.ezacrosscontrol.control.keyboard

import android.inputmethodservice.InputMethodService
import android.view.KeyEvent
import android.view.View
import android.widget.FrameLayout
import android.view.inputmethod.EditorInfo

class EZAcrossInputMethodService : InputMethodService() {

    companion object {
        private var instance: EZAcrossInputMethodService? = null

        fun commitText(text: String) {
            instance?.currentInputConnection?.commitText(text, 1)
        }

        fun sendSpecialKey(keyCode: Int) {
            instance?.currentInputConnection?.let { ic ->
                ic.sendKeyEvent(KeyEvent(KeyEvent.ACTION_DOWN, keyCode))
                ic.sendKeyEvent(KeyEvent(KeyEvent.ACTION_UP, keyCode))
            }
        }

        fun deleteSurroundingText(beforeLength: Int, afterLength: Int) {
            instance?.currentInputConnection?.deleteSurroundingText(beforeLength, afterLength)
        }
        
        fun performEditorAction(actionCode: Int) {
            instance?.currentInputConnection?.performEditorAction(actionCode)
        }

        fun performContextMenuAction(id: Int) {
            instance?.currentInputConnection?.performContextMenuAction(id)
        }

        fun isReady(): Boolean {
            return instance != null && instance?.currentInputConnection != null
        }
    }

    override fun onCreate() {
        super.onCreate()
        instance = this
    }

    override fun onDestroy() {
        super.onDestroy()
        if (instance == this) {
            instance = null
        }
    }

    override fun onStartInputView(info: EditorInfo?, restarting: Boolean) {
        super.onStartInputView(info, restarting)
        // Keep a reference to the active editor info if needed for Enter actions
    }

    override fun onCreateInputView(): View {
        // Return a minimal transparent view, we don't want an on-screen keyboard
        val view = FrameLayout(this)
        view.layoutParams = FrameLayout.LayoutParams(0, 0)
        return view
    }

    override fun onEvaluateInputViewShown(): Boolean {
        // Always false to prevent a visual keyboard from appearing
        return false
    }
}
