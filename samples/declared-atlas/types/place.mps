<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="b1000000-0000-4000-9002-000000000010" name="place">
  <nodes>
    <node id="b1000000-0000-4000-8002-000000000010" concept="TypeRule.Named" name="place">
      <properties>
        <name>place</name>
      </properties>
      <children role="type">
        <node id="b1000000-0000-4000-8002-000000000110" concept="TypeRule.Record">
          <children role="fields">
            <node id="b1000000-0000-4000-8002-000000000210" concept="TypeRule.Field" name="name">
              <properties>
                <name>name</name>
              </properties>
              <children role="type">
                <node id="b1000000-0000-4000-8002-000000000310" concept="TypeRule.Primitive" name="string">
                  <properties>
                    <name>string</name>
                  </properties>
                </node>
              </children>
            </node>
            <node id="b1000000-0000-4000-8002-000000000211" concept="TypeRule.Field" name="at">
              <properties>
                <name>at</name>
              </properties>
              <children role="type">
                <node id="b1000000-0000-4000-8002-000000000311" concept="TypeRule.Primitive" name="string">
                  <properties>
                    <name>string</name>
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