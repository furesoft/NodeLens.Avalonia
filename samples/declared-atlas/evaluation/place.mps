<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="b1000000-0000-4000-9006-000000000010" name="place">
  <nodes>
    <node id="b1000000-0000-4000-8006-000000000010" concept="Evaluation.Rule" name="Atlas.Place">
      <properties>
        <conceptKey>Atlas.Place</conceptKey>
      </properties>
      <children role="body">
        <node id="b1000000-0000-4000-8006-000000000110" concept="Evaluation.Choose">
          <children role="condition">
            <node id="b1000000-0000-4000-8006-000000000111" concept="Evaluation.Has" name="at">
              <properties>
                <slot>at</slot>
              </properties>
            </node>
          </children>
          <children role="then">
            <node id="b1000000-0000-4000-8006-000000000114" concept="Evaluation.Operator" name="operatorSlot">
              <properties>
                <symbol>+</symbol>
              </properties>
              <children role="left">
                <node id="b1000000-0000-4000-8006-000000000115" concept="Evaluation.Operator" name="operatorSlot">
                  <properties>
                    <symbol>+</symbol>
                  </properties>
                  <children role="left">
                    <node id="b1000000-0000-4000-8006-000000000116" concept="Evaluation.Property" name="name">
                      <properties>
                        <slot>name</slot>
                      </properties>
                    </node>
                  </children>
                  <children role="right">
                    <node id="b1000000-0000-4000-8006-000000000117" concept="Evaluation.Constant" name="at ">
                      <properties>
                        <kind>string</kind>
                        <value> at </value>
                      </properties>
                    </node>
                  </children>
                </node>
              </children>
              <children role="right">
                <node id="b1000000-0000-4000-8006-000000000118" concept="Evaluation.Property" name="at">
                  <properties>
                    <slot>at</slot>
                  </properties>
                </node>
              </children>
            </node>
          </children>
          <children role="otherwise">
            <node id="b1000000-0000-4000-8006-000000000119" concept="Evaluation.Property" name="name">
              <properties>
                <slot>name</slot>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>