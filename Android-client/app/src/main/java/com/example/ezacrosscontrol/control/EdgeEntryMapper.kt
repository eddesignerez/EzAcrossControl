package com.example.ezacrosscontrol.control

/** Pure proportional mapping shared by all four entry edges. */
internal object EdgeEntryMapper {
    fun coordinate(length: Int, normalized: Double): Float {
        if (length <= 1) return 0f
        return ((length - 1) * normalized.coerceIn(0.0, 1.0)).toFloat()
    }
}
