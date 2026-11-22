<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="f7000000-0000-4000-8008-000000000001" name="lines">
  <nodes>
    <node id="f7000000-0000-4000-8008-000000000002" concept="Invoice.Line" name="Pens">
      <properties>
        <label>Pens</label>
      </properties>
      <children role="quantity">
        <node id="f7000000-0000-4000-8008-000000000003" concept="Units.Quantity">
          <properties>
            <value>3</value>
            <unit>pcs</unit>
          </properties>
        </node>
      </children>
    </node>
    <node id="f7000000-0000-4000-8008-000000000004" concept="Invoice.Line" name="Paper">
      <properties>
        <label>Paper</label>
      </properties>
      <children role="quantity">
        <node id="f7000000-0000-4000-8008-000000000005" concept="Units.Quantity">
          <properties>
            <value>500</value>
            <unit>sheet</unit>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>