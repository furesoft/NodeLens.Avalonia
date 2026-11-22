<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="f2000000-0000-4000-9000-000000000001" name="line">
  <nodes>
    <node id="f2000000-0000-4000-8000-000000000001" concept="Evaluation.Rule" name="Invoice.Line">
      <properties>
        <conceptKey>Invoice.Line</conceptKey>
      </properties>
      <children role="body">
        <node id="f2000000-0000-4000-8000-000000000002" concept="Evaluation.Field" name="value">
          <properties>
            <name>value</name>
          </properties>
          <children role="from">
            <node id="f2000000-0000-4000-8000-000000000003" concept="Evaluation.Child" name="quantity">
              <properties>
                <slot>quantity</slot>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>