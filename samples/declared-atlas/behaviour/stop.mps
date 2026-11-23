<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="b1000000-0000-4000-9007-000000000001" name="stop">
  <nodes>
    <node id="b1000000-0000-4000-8007-000000000001" concept="Behaviour.Document" name="Atlas.Stop">
      <properties>
        <concept>Atlas.Stop</concept>
      </properties>
      <children role="functions">
        <node id="b1000000-0000-4000-8007-000000000002" concept="Behaviour.Function">
          <properties>
            <kind>name</kind>
          </properties>
          <children role="body">
            <node id="b1000000-0000-4000-8007-000000000003" concept="Behaviour.Reference">
              <properties>
                <slot>to</slot>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>