package com.example.ezacrosscontrol.control

import org.junit.Assert.assertEquals
import org.junit.Test

class EdgeEntryMapperTest {
    @Test
    fun coordinate_clampsAndPreservesProportionalPosition() {
        assertEquals(0f, EdgeEntryMapper.coordinate(1080, -1.0))
        assertEquals(269.75f, EdgeEntryMapper.coordinate(1080, .25))
        assertEquals(1079f, EdgeEntryMapper.coordinate(1080, 2.0))
    }
}
