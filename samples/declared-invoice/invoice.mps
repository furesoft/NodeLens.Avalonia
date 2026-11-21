<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="f0000000-0000-4000-9000-000000000004" name="invoice">
  <nodes>
    <node id="f0000000-0000-4000-8000-000000000004" concept="Invoice.Line" name="Paper">
      <properties>
        <label>Paper</label>
      </properties>
      <children role="quantity">
        <node id="f0000000-0000-4000-8000-000000000104" concept="Units.Quantity">
          <properties>
            <value>12</value>
            <unit> kg </unit>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>