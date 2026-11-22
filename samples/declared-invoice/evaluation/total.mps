<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="f2000000-0000-4000-9000-000000000002" name="total">
  <nodes>
    <node id="f2000000-0000-4000-8000-000000000010" concept="Evaluation.Rule" name="Invoice.Bill">
      <properties>
        <conceptKey>Invoice.Bill</conceptKey>
      </properties>
      <children role="body">
        <node id="f2000000-0000-4000-8000-000000000011" concept="Evaluation.Sum" name="lines">
          <properties>
            <over>lines</over>
          </properties>
          <children role="each">
            <node id="f2000000-0000-4000-8000-000000000012" concept="Evaluation.Field" name="value">
              <properties>
                <name>value</name>
              </properties>
              <children role="from">
                <node id="f2000000-0000-4000-8000-000000000013" concept="Evaluation.Child" name="quantity">
                  <properties>
                    <slot>quantity</slot>
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