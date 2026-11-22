<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="b2000000-0000-4000-9003-000000000002" name="stop-target">
  <nodes>
    <node id="b2000000-0000-4000-8003-000000000002" concept="Constraint.Rule" name="this stop points at no place.">
      <properties>
        <conceptKey>Atlas.Stop</conceptKey>
        <message>this stop points at no place.</message>
        <severity>error</severity>
        <code>atlas.stop.target</code>
      </properties>
      <children role="body">
        <node id="b2000000-0000-4000-8003-000000000102" concept="Constraint.EmptyReference">
          <properties>
            <slot>to</slot>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>