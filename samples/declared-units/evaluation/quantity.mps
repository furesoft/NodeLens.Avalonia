<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="e1000000-0000-4000-9000-000000000001" name="quantity">
  <nodes>
    <node id="e1000000-0000-4000-8000-000000000001" concept="Evaluation.Rule" name="Units.Quantity">
      <properties>
        <conceptKey>Units.Quantity</conceptKey>
      </properties>
      <children role="body">
        <node id="e1000000-0000-4000-8000-000000000002" concept="Evaluation.Record">
          <children role="fields">
            <node id="e1000000-0000-4000-8000-000000000003" concept="Evaluation.RecordField" name="value">
              <properties>
                <name>value</name>
              </properties>
              <children role="value">
                <node id="e1000000-0000-4000-8000-000000000004" concept="Evaluation.Property" name="value">
                  <properties>
                    <slot>value</slot>
                  </properties>
                </node>
              </children>
            </node>
            <node id="e1000000-0000-4000-8000-000000000005" concept="Evaluation.RecordField" name="unit">
              <properties>
                <name>unit</name>
              </properties>
              <children role="value">
                <node id="e1000000-0000-4000-8000-000000000006" concept="Evaluation.Property" name="unit">
                  <properties>
                    <slot>unit</slot>
                  </properties>
                </node>
              </children>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>