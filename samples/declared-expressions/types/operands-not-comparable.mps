<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="d0000000-0000-4000-9002-000000000005" name="operands-not-comparable">
  <nodes>
    <node id="d0000000-0000-4000-8002-000000000005" concept="TypeRule.Rule" name="Decl.Binary">
      <properties>
        <conceptKey>Decl.Binary</conceptKey>
      </properties>
      <children role="body">
        <node id="d0000000-0000-4000-8002-000000000115" concept="TypeRule.Complain" name="The operands are not comparable, so no value of one could be used as the other.">
          <properties>
            <message>The operands are not comparable, so no value of one could be used as the other.</message>
            <severity>error</severity>
            <code>decl.type.operands</code>
          </properties>
          <children role="when">
            <node id="d0000000-0000-4000-8002-000000000215" concept="TypeRule.NotComparable" name="left">
              <properties>
                <left>left</left>
                <right>right</right>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>