<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="f0000000-0000-4000-9000-000000000007" name="bill">
  <nodes>
    <node id="f0000000-0000-4000-8000-000000000007" concept="Invoice.Bill" name="March">
      <properties>
        <name>March</name>
      </properties>
      <children role="lines">
        <node id="f0000000-0000-4000-8000-000000000107" concept="Invoice.Line" name="Paper">
          <properties>
            <label>Paper</label>
          </properties>
          <children role="quantity">
            <node id="f0000000-0000-4000-8000-000000000207" concept="Units.Quantity">
              <properties>
                <value>12</value>
                <unit>kg</unit>
              </properties>
            </node>
          </children>
        </node>
        <node id="f0000000-0000-4000-8000-000000000307" concept="Invoice.Line" name="Ink">
          <properties>
            <label>Ink</label>
          </properties>
          <children role="quantity">
            <node id="f0000000-0000-4000-8000-000000000407" concept="Units.Quantity">
              <properties>
                <value>5</value>
                <unit>l</unit>
              </properties>
            </node>
          </children>
        </node>
        <node id="f0000000-0000-4000-8000-000000000507" concept="Invoice.Line" name="Pens">
          <properties>
            <label>Pens</label>
          </properties>
          <children role="quantity">
            <node id="f0000000-0000-4000-8000-000000000607" concept="Units.Quantity">
              <properties>
                <value>30</value>
                <unit>piece</unit>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>