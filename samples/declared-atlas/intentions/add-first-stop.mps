<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="b3000000-0000-4000-9005-000000000001" name="add-first-stop">
  <nodes>
    <node id="b3000000-0000-4000-8005-000000000001" concept="Intent.Rule" name="Add the first stop">
      <properties>
        <conceptKey>Atlas.Route</conceptKey>
        <label>Add the first stop</label>
        <description>Puts a stop into the empty place of the route.</description>
      </properties>
      <children role="action">
        <node id="b3000000-0000-4000-8005-000000000101" concept="Intent.Insert">
          <properties>
            <concept>Atlas.Stop</concept>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>