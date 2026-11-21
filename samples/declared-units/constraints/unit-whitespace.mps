<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="e0000000-0000-4000-9000-000000000004" name="unit-whitespace">
  <nodes>
    <node id="e0000000-0000-4000-8000-000000000004" concept="Constraint.Rule" name="the unit starts or ends with whitespace, which reads as part of it.">
      <properties>
        <conceptKey>Units.Quantity</conceptKey>
        <message>the unit starts or ends with whitespace, which reads as part of it.</message>
        <severity>warning</severity>
        <code>units.quantity.unit</code>
      </properties>
      <children role="body">
        <node id="e0000000-0000-4000-8000-000000000104" concept="Constraint.SurroundingWhitespace">
          <properties>
            <slot>unit</slot>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>